using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;

namespace McpGateway.Domain.Auditing;

/// <summary>
/// One immutable, append-only record of a security event. Captures who acted, on
/// what, the result, and the correlating trace id. Sensitive request context is
/// summarized as a hash (<see cref="RequestHash"/>) rather than stored raw, so the
/// trail stays useful without becoming a place secrets leak.
/// </summary>
public sealed class AuditEntry
{
    public Guid Id { get; private set; }
    public DateTimeOffset OccurredAt { get; private set; }

    /// <summary>Correlation id (the request's trace id) tying this event to others in the same operation.</summary>
    public string TraceId { get; private set; } = null!;

    public AuditEventType EventType { get; private set; }

    /// <summary>Principal that performed the action.</summary>
    public ClientId ActorClientId { get; private set; } = null!;
    public IdentityType ActorType { get; private set; }

    public ToolName? ToolName { get; private set; }
    public ToolVersionNumber? Version { get; private set; }

    /// <summary>Outcome of the event, e.g. <c>Permitted</c>, <c>Denied</c>, <c>Approved</c>.</summary>
    public string Result { get; private set; } = null!;

    /// <summary>Non-sensitive elaboration such as reason codes; never raw payloads or secrets.</summary>
    public string? Detail { get; private set; }

    /// <summary>Hash of the canonical request context (the "input hash"); null when not applicable.</summary>
    public string? RequestHash { get; private set; }

    /// <summary>The approval this event concerns, when relevant.</summary>
    public Guid? ApprovalId { get; private set; }

    private AuditEntry()
    {
        // EF Core materialization only.
    }

    public static AuditEntry Create(
        Guid id,
        DateTimeOffset occurredAt,
        string traceId,
        AuditEventType eventType,
        ClientId actorClientId,
        IdentityType actorType,
        string result,
        ToolName? toolName = null,
        ToolVersionNumber? version = null,
        string? detail = null,
        string? requestHash = null,
        Guid? approvalId = null) =>
        new()
        {
            Id = id,
            OccurredAt = occurredAt,
            TraceId = traceId,
            EventType = eventType,
            ActorClientId = actorClientId,
            ActorType = actorType,
            Result = result,
            ToolName = toolName,
            Version = version,
            Detail = detail,
            RequestHash = requestHash,
            ApprovalId = approvalId,
        };
}
