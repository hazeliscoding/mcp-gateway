namespace McpGateway.Domain.Auditing;

/// <summary>The kind of security event an <see cref="AuditEntry"/> records.</summary>
public enum AuditEventType
{
    /// <summary>An authorization decision was evaluated (permit, deny, requires-approval, or prohibited).</summary>
    AuthorizationDecision,

    /// <summary>A caller opened an approval request.</summary>
    ApprovalRequested,

    /// <summary>An approver approved a pending request.</summary>
    ApprovalApproved,

    /// <summary>An approver rejected a pending request.</summary>
    ApprovalRejected,
}
