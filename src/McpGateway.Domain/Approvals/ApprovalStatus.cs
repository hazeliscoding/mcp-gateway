namespace McpGateway.Domain.Approvals;

/// <summary>Lifecycle state of an approval request.</summary>
public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected,
}
