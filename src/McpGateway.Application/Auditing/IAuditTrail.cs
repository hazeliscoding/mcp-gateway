using McpGateway.Application.Authorization;
using McpGateway.Domain.Approvals;
using McpGateway.Domain.Auditing;
using McpGateway.Domain.Authorization;
using McpGateway.Domain.Tools;

namespace McpGateway.Application.Auditing;

/// <summary>
/// The write side of the audit trail, injected into the decision services. Keeping
/// it an interface lets those services record events without depending on hashing,
/// trace, or persistence concerns — and lets tests observe what was recorded.
/// </summary>
public interface IAuditTrail
{
    /// <summary>Records one authorization decision. <paramref name="canonicalInput"/> is hashed, not stored raw.</summary>
    Task RecordAuthorizationAsync(
        CallerPrincipal caller,
        ToolName toolName,
        ToolVersionNumber? version,
        ToolAction action,
        string canonicalInput,
        AuthorizationOutcome outcome,
        IReadOnlyList<AuthorizationReasonCode> reasonCodes,
        CancellationToken cancellationToken);

    /// <summary>Records an approval lifecycle event (requested, approved, or rejected).</summary>
    Task RecordApprovalAsync(
        AuditEventType eventType,
        CallerPrincipal actor,
        ApprovalRequest approval,
        CancellationToken cancellationToken);
}
