using McpGateway.Domain.Auditing;

namespace McpGateway.Application.Auditing;

/// <summary>Filters for querying the audit trail. Results come back newest-first.</summary>
/// <param name="ToolName">Restrict to one tool.</param>
/// <param name="Actor">Restrict to one actor client id.</param>
/// <param name="EventType">Restrict to one event type.</param>
/// <param name="From">Inclusive lower bound on <see cref="AuditEntry.OccurredAt"/>.</param>
/// <param name="To">Inclusive upper bound on <see cref="AuditEntry.OccurredAt"/>.</param>
/// <param name="Limit">Maximum rows to return; capped by the service.</param>
public sealed record AuditQueryFilter(
    string? ToolName = null,
    string? Actor = null,
    AuditEventType? EventType = null,
    DateTimeOffset? From = null,
    DateTimeOffset? To = null,
    int Limit = 100);

/// <summary>An audit entry as returned to callers.</summary>
public sealed record AuditEntryResponse(
    Guid Id,
    DateTimeOffset OccurredAt,
    string TraceId,
    AuditEventType EventType,
    string ActorClientId,
    string Result,
    string? ToolName,
    string? Version,
    string? Detail,
    string? RequestHash,
    Guid? ApprovalId);
