using McpGateway.Application.Authorization;
using McpGateway.Domain.Approvals;
using McpGateway.Domain.Auditing;
using McpGateway.Domain.Authorization;
using McpGateway.Domain.Tools;

namespace McpGateway.Application.Auditing;

/// <summary>
/// Writes and reads the audit trail. Recording stamps each entry with the current
/// time and trace id and hashes the request context; it saves its own entry so an
/// otherwise read-only decision (authorize) is still recorded.
/// </summary>
public sealed class AuditService(
    IAuditRepository repository,
    ITraceContext traceContext,
    IPayloadHasher payloadHasher,
    TimeProvider timeProvider) : IAuditTrail
{
    private const int MaxLimit = 500;
    private static readonly TimeSpan DefaultStatsWindow = TimeSpan.FromDays(7);

    public Task RecordAuthorizationAsync(
        CallerPrincipal caller,
        ToolName toolName,
        ToolVersionNumber? version,
        ToolAction action,
        string canonicalInput,
        AuthorizationOutcome outcome,
        IReadOnlyList<AuthorizationReasonCode> reasonCodes,
        CancellationToken cancellationToken)
    {
        var entry = AuditEntry.Create(
            Guid.NewGuid(),
            timeProvider.GetUtcNow(),
            traceContext.CurrentTraceId,
            AuditEventType.AuthorizationDecision,
            caller.ClientId,
            caller.Type,
            outcome.ToString(),
            toolName,
            version,
            detail: reasonCodes.Count == 0 ? null : string.Join(",", reasonCodes),
            requestHash: payloadHasher.Hash(canonicalInput));

        return AppendAsync(entry, cancellationToken);
    }

    public Task RecordApprovalAsync(
        AuditEventType eventType,
        CallerPrincipal actor,
        ApprovalRequest approval,
        CancellationToken cancellationToken)
    {
        var canonicalInput = string.Join('|',
            approval.ToolName, approval.Version, approval.Action, approval.Environment, approval.Resource ?? "-");

        var entry = AuditEntry.Create(
            Guid.NewGuid(),
            timeProvider.GetUtcNow(),
            traceContext.CurrentTraceId,
            eventType,
            actor.ClientId,
            actor.Type,
            ResultFor(eventType),
            approval.ToolName,
            approval.Version,
            detail: approval.DecisionNote,
            requestHash: payloadHasher.Hash(canonicalInput),
            approvalId: approval.Id);

        return AppendAsync(entry, cancellationToken);
    }

    public async Task<OperationResult<IReadOnlyList<AuditEntryResponse>>> ListAsync(
        AuditQueryFilter filter, CancellationToken cancellationToken)
    {
        var bounded = filter with { Limit = Math.Clamp(filter.Limit, 1, MaxLimit) };
        var entries = await repository.QueryAsync(bounded, cancellationToken);
        IReadOnlyList<AuditEntryResponse> responses = entries.Select(ToResponse).ToList();
        return OperationResult<IReadOnlyList<AuditEntryResponse>>.Success(responses);
    }

    public async Task<OperationResult<AuditStatsResponse>> GetStatsAsync(
        AuditStatsFilter filter, CancellationToken cancellationToken)
    {
        var to = filter.To ?? timeProvider.GetUtcNow();
        var from = filter.From ?? to - DefaultStatsWindow;
        if (from > to)
        {
            return OperationResult<AuditStatsResponse>.Invalid("The stats window start must not be after its end.");
        }

        var stats = await repository.GetStatsAsync(from, to, cancellationToken);
        return OperationResult<AuditStatsResponse>.Success(stats);
    }

    private async Task AppendAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        await repository.AddAsync(entry, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static string ResultFor(AuditEventType eventType) => eventType switch
    {
        AuditEventType.ApprovalRequested => "Requested",
        AuditEventType.ApprovalApproved => "Approved",
        AuditEventType.ApprovalRejected => "Rejected",
        _ => eventType.ToString(),
    };

    private static AuditEntryResponse ToResponse(AuditEntry entry) =>
        new(
            entry.Id,
            entry.OccurredAt,
            entry.TraceId,
            entry.EventType,
            entry.ActorClientId.Value,
            entry.Result,
            entry.ToolName?.Value,
            entry.Version?.ToString(),
            entry.Detail,
            entry.RequestHash,
            entry.ApprovalId);
}
