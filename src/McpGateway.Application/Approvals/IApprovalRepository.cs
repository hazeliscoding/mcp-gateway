using McpGateway.Domain.Approvals;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;

namespace McpGateway.Application.Approvals;

/// <summary>Persistence boundary for approval requests.</summary>
public interface IApprovalRepository
{
    Task<ApprovalRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lists requests, optionally filtered to a single status, newest first.</summary>
    Task<IReadOnlyList<ApprovalRequest>> ListAsync(ApprovalStatus? status, CancellationToken cancellationToken);

    /// <summary>
    /// Whether a request in the given status exists for the exact
    /// (requester, tool, version) tuple. Used to detect a standing approval grant
    /// and to prevent duplicate pending requests.
    /// </summary>
    Task<bool> ExistsAsync(
        ClientId requester,
        ToolName toolName,
        ToolVersionNumber version,
        ApprovalStatus status,
        CancellationToken cancellationToken);

    Task AddAsync(ApprovalRequest approval, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
