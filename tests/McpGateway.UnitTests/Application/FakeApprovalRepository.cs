using McpGateway.Application.Approvals;
using McpGateway.Domain.Approvals;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Application;

/// <summary>In-memory approval store; mutations of returned aggregates are visible immediately.</summary>
internal sealed class FakeApprovalRepository : IApprovalRepository
{
    private readonly Dictionary<Guid, ApprovalRequest> _approvals = [];

    public int SaveCount { get; private set; }

    public Task<ApprovalRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(_approvals.GetValueOrDefault(id));

    public Task<IReadOnlyList<ApprovalRequest>> ListAsync(ApprovalStatus? status, CancellationToken cancellationToken)
    {
        IReadOnlyList<ApprovalRequest> results = _approvals.Values
            .Where(a => status == null || a.Status == status)
            .OrderByDescending(a => a.RequestedAt)
            .ToList();
        return Task.FromResult(results);
    }

    public Task<bool> ExistsAsync(
        ClientId requester, ToolName toolName, ToolVersionNumber version, ApprovalStatus status,
        CancellationToken cancellationToken) =>
        Task.FromResult(_approvals.Values.Any(a =>
            a.RequesterClientId == requester
            && a.ToolName == toolName
            && a.Version == version
            && a.Status == status));

    public Task AddAsync(ApprovalRequest approval, CancellationToken cancellationToken)
    {
        _approvals.Add(approval.Id, approval);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
