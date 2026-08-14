using McpGateway.Application.Auditing;
using McpGateway.Application.Authorization;
using McpGateway.Application.Tools;
using McpGateway.Domain;
using McpGateway.Domain.Approvals;
using McpGateway.Domain.Auditing;
using McpGateway.Domain.Authorization;
using McpGateway.Domain.Tools;
using Microsoft.Extensions.Logging;

namespace McpGateway.Application.Approvals;

/// <summary>
/// Commands and queries for the approval workflow. Enforces that a request only
/// exists for a version that actually needs approval, delegates the four-eyes rule
/// and status transitions to the <see cref="ApprovalRequest"/> aggregate, and records
/// each lifecycle event to the audit trail.
/// </summary>
public sealed class ApprovalService(
    IApprovalRepository approvals,
    IToolRegistryRepository tools,
    IAuditTrail auditTrail,
    TimeProvider timeProvider,
    ILogger<ApprovalService> logger)
{
    private const string DefaultEnvironment = "production";

    public async Task<OperationResult<ApprovalResponse>> RequestApprovalAsync(
        string toolName, RequestApprovalCommand command, CallerPrincipal requester, CancellationToken cancellationToken)
    {
        ToolName name;
        ToolVersionNumber? requestedVersion;
        try
        {
            name = ToolName.Create(toolName);
            requestedVersion = string.IsNullOrWhiteSpace(command.Version)
                ? null
                : ToolVersionNumber.Create(command.Version);
        }
        catch (DomainException ex)
        {
            return OperationResult<ApprovalResponse>.Invalid(ex.Message);
        }

        var tool = await tools.GetByNameAsync(name, cancellationToken);
        if (tool is null)
        {
            return OperationResult<ApprovalResponse>.NotFound($"Tool '{name}' is not registered.");
        }

        var version = ResolveVersion(tool, requestedVersion);
        if (version is null)
        {
            return OperationResult<ApprovalResponse>.Invalid($"Tool '{name}' has no matching version to approve.");
        }

        switch (RiskPolicy.Classify(version.RiskLevel, version.ApprovalRequired))
        {
            case RiskDisposition.Automatic:
                return OperationResult<ApprovalResponse>.Invalid(
                    $"Version {version.Number} of '{name}' runs automatically and does not require approval.");
            case RiskDisposition.Prohibited:
                return OperationResult<ApprovalResponse>.Invalid(
                    $"Version {version.Number} of '{name}' is destructive and cannot be approved.");
        }

        if (await approvals.ExistsAsync(requester.ClientId, name, version.Number, ApprovalStatus.Pending, cancellationToken))
        {
            return OperationResult<ApprovalResponse>.Conflict(
                $"An approval request for '{name}' version {version.Number} is already pending for {requester.ClientId.Value}.");
        }

        var approval = ApprovalRequest.Open(
            Guid.NewGuid(),
            name,
            version.Number,
            requester.ClientId,
            version.RiskLevel,
            command.Action,
            command.Environment?.Trim() is { Length: > 0 } env ? env : DefaultEnvironment,
            command.Resource,
            timeProvider.GetUtcNow());

        await approvals.AddAsync(approval, cancellationToken);
        await approvals.SaveChangesAsync(cancellationToken);

        await auditTrail.RecordApprovalAsync(AuditEventType.ApprovalRequested, requester, approval, cancellationToken);

        logger.LogInformation(
            "Opened approval {ApprovalId} for {ClientId} on tool {ToolName} version {Version}",
            approval.Id, requester.ClientId.Value, name.Value, version.Number);
        return OperationResult<ApprovalResponse>.Success(ToResponse(approval));
    }

    public Task<OperationResult<ApprovalResponse>> ApproveAsync(
        Guid id, CallerPrincipal approver, string? note, CancellationToken cancellationToken) =>
        DecideAsync(id, approver, note, cancellationToken, AuditEventType.ApprovalApproved,
            (approval, utcNow) => approval.Approve(approver.ClientId, note, utcNow), "approved");

    public Task<OperationResult<ApprovalResponse>> RejectAsync(
        Guid id, CallerPrincipal approver, string? note, CancellationToken cancellationToken) =>
        DecideAsync(id, approver, note, cancellationToken, AuditEventType.ApprovalRejected,
            (approval, utcNow) => approval.Reject(approver.ClientId, note, utcNow), "rejected");

    public async Task<OperationResult<ApprovalResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var approval = await approvals.GetByIdAsync(id, cancellationToken);
        return approval is null
            ? OperationResult<ApprovalResponse>.NotFound($"Approval request {id} was not found.")
            : OperationResult<ApprovalResponse>.Success(ToResponse(approval));
    }

    public async Task<OperationResult<IReadOnlyList<ApprovalResponse>>> ListAsync(
        ApprovalStatus? status, CancellationToken cancellationToken)
    {
        var results = await approvals.ListAsync(status, cancellationToken);
        IReadOnlyList<ApprovalResponse> responses = results.Select(ToResponse).ToList();
        return OperationResult<IReadOnlyList<ApprovalResponse>>.Success(responses);
    }

    private async Task<OperationResult<ApprovalResponse>> DecideAsync(
        Guid id, CallerPrincipal approver, string? note, CancellationToken cancellationToken,
        AuditEventType eventType, Action<ApprovalRequest, DateTimeOffset> decide, string decisionVerb)
    {
        var approval = await approvals.GetByIdAsync(id, cancellationToken);
        if (approval is null)
        {
            return OperationResult<ApprovalResponse>.NotFound($"Approval request {id} was not found.");
        }

        try
        {
            decide(approval, timeProvider.GetUtcNow());
        }
        catch (DomainConflictException ex)
        {
            return OperationResult<ApprovalResponse>.Conflict(ex.Message);
        }
        catch (DomainException ex)
        {
            return OperationResult<ApprovalResponse>.Invalid(ex.Message);
        }

        await approvals.SaveChangesAsync(cancellationToken);
        await auditTrail.RecordApprovalAsync(eventType, approver, approval, cancellationToken);

        logger.LogInformation(
            "Approval {ApprovalId} {Decision} by {ClientId}", approval.Id, decisionVerb, approver.ClientId.Value);
        return OperationResult<ApprovalResponse>.Success(ToResponse(approval));
    }

    private static ToolVersion? ResolveVersion(ToolDefinition tool, ToolVersionNumber? requestedVersion) =>
        requestedVersion is null
            ? tool.Versions.LastOrDefault(v => v.Status == ToolVersionStatus.Active)
            : tool.Versions.FirstOrDefault(v => v.Number == requestedVersion);

    private static ApprovalResponse ToResponse(ApprovalRequest approval) =>
        new(
            approval.Id,
            approval.ToolName.Value,
            approval.Version.ToString(),
            approval.RequesterClientId.Value,
            approval.RiskLevel,
            approval.Action,
            approval.Environment,
            approval.Resource,
            approval.Status,
            approval.RequestedAt,
            approval.DecidedAt,
            approval.DecidedBy?.Value,
            approval.DecisionNote);
}
