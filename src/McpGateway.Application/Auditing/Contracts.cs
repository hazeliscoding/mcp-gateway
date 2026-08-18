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

/// <summary>Window for aggregating audit statistics. Both bounds are optional; the service defaults them.</summary>
/// <param name="From">Inclusive lower bound; defaults to seven days before <paramref name="To"/>.</param>
/// <param name="To">Inclusive upper bound; defaults to now.</param>
public sealed record AuditStatsFilter(DateTimeOffset? From = null, DateTimeOffset? To = null);

/// <summary>A label paired with how many audit events carried it.</summary>
public sealed record NamedCount(string Name, int Count);

/// <summary>A UTC day paired with how many audit events fell on it.</summary>
public sealed record DailyCount(DateOnly Date, int Count);

/// <summary>Aggregated audit activity over a resolved time window.</summary>
/// <param name="From">Resolved inclusive lower bound of the window.</param>
/// <param name="To">Resolved inclusive upper bound of the window.</param>
/// <param name="TotalEvents">Total events in the window.</param>
/// <param name="EventsByType">Counts per <see cref="AuditEventType"/>, descending.</param>
/// <param name="EventsByTool">Counts per tool (top ten), descending.</param>
/// <param name="AuthorizationOutcomes">Counts per outcome across authorization decisions, descending.</param>
/// <param name="EventsByActor">Counts per acting client (top ten), descending.</param>
/// <param name="EventsPerDay">Counts per UTC day the window covers, ascending.</param>
public sealed record AuditStatsResponse(
    DateTimeOffset From,
    DateTimeOffset To,
    int TotalEvents,
    IReadOnlyList<NamedCount> EventsByType,
    IReadOnlyList<NamedCount> EventsByTool,
    IReadOnlyList<NamedCount> AuthorizationOutcomes,
    IReadOnlyList<NamedCount> EventsByActor,
    IReadOnlyList<DailyCount> EventsPerDay);

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
