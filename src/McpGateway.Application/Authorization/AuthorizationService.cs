using McpGateway.Application.Approvals;
using McpGateway.Application.Auditing;
using McpGateway.Application.Tools;
using McpGateway.Domain;
using McpGateway.Domain.Approvals;
using McpGateway.Domain.Authorization;
using McpGateway.Domain.Tools;
using Microsoft.Extensions.Logging;

namespace McpGateway.Application.Authorization;

/// <summary>
/// Orchestrates an authorization decision: loads the target tool, resolves the
/// requested version, checks for a standing approval grant, and hands the assembled
/// attributes to the deterministic <see cref="AuthorizationPolicy"/>. It maps
/// decisions to responses but makes none of the permit/deny rules itself, and
/// records every evaluated decision to the audit trail.
/// </summary>
public sealed class AuthorizationService(
    IToolRegistryRepository repository,
    IApprovalRepository approvals,
    IAuditTrail auditTrail,
    ILogger<AuthorizationService> logger)
{
    private const string DefaultEnvironment = "production";

    public async Task<OperationResult<AuthorizationDecisionResponse>> AuthorizeToolAsync(
        string toolName,
        AuthorizeToolRequest request,
        CallerPrincipal caller,
        CancellationToken cancellationToken)
    {
        ToolName name;
        ToolVersionNumber? requestedVersion;
        try
        {
            name = ToolName.Create(toolName);
            requestedVersion = string.IsNullOrWhiteSpace(request.Version)
                ? null
                : ToolVersionNumber.Create(request.Version);
        }
        catch (DomainException ex)
        {
            return OperationResult<AuthorizationDecisionResponse>.Invalid(ex.Message);
        }

        var tool = await repository.GetByNameAsync(name, cancellationToken);
        if (tool is null)
        {
            // The tool genuinely does not exist — distinct from "exists but denied".
            return OperationResult<AuthorizationDecisionResponse>.NotFound($"Tool '{name}' is not registered.");
        }

        var version = ResolveVersion(tool, requestedVersion);

        // A standing approval for this exact (caller, tool, version) upgrades an
        // approval-gated outcome to Permitted.
        var approvalGranted = version is not null
            && await approvals.ExistsAsync(
                caller.ClientId, tool.Name, version.Number, ApprovalStatus.Approved, cancellationToken);

        var environment = request.Environment?.Trim() is { Length: > 0 } env ? env : DefaultEnvironment;
        var decision = AuthorizationPolicy.Evaluate(new AuthorizationRequest(
            caller.ClientId,
            caller.Type,
            caller.GrantedScopes,
            tool.Name,
            tool.Enabled,
            version,
            request.Action,
            environment,
            request.Resource,
            approvalGranted));

        var reportedVersion = version?.Number.ToString() ?? requestedVersion?.ToString();
        logger.LogInformation(
            "Authorization {Outcome} for {IdentityType} {ClientId} on tool {ToolName} version {Version} action {Action}: {ReasonCodes}",
            decision.Outcome,
            caller.Type, caller.ClientId.Value, tool.Name.Value, reportedVersion ?? "unresolved",
            request.Action, string.Join(",", decision.Reasons.Select(r => r.Code)));

        // Record the decision. The canonical input is hashed, not stored raw.
        await auditTrail.RecordAuthorizationAsync(
            caller, tool.Name, version?.Number, request.Action,
            $"{tool.Name}|{reportedVersion ?? "-"}|{request.Action}|{environment}|{request.Resource ?? "-"}",
            decision.Outcome, decision.Reasons.Select(r => r.Code).ToList(), cancellationToken);

        return OperationResult<AuthorizationDecisionResponse>.Success(new AuthorizationDecisionResponse(
            decision.Outcome,
            decision.Permit,
            tool.Name.Value,
            reportedVersion,
            request.Action,
            decision.Reasons.Select(r => new ReasonResponse(r.Code, r.Message)).ToList()));
    }

    /// <summary>
    /// Picks the version the decision concerns: the exact requested version if
    /// given, otherwise the highest active (non-deprecated) version. Returns
    /// <see langword="null"/> when nothing matches, which the policy denies.
    /// </summary>
    private static ToolVersion? ResolveVersion(ToolDefinition tool, ToolVersionNumber? requestedVersion) =>
        requestedVersion is null
            ? tool.Versions.LastOrDefault(v => v.Status == ToolVersionStatus.Active)
            : tool.Versions.FirstOrDefault(v => v.Number == requestedVersion);
}
