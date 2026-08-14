using McpGateway.Application.Approvals;
using McpGateway.Domain.Approvals;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;
using Microsoft.EntityFrameworkCore;

namespace McpGateway.Infrastructure.Persistence;

public sealed class ApprovalRepository(McpGatewayDbContext dbContext) : IApprovalRepository
{
    public async Task<ApprovalRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        await dbContext.Approvals.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public async Task<IReadOnlyList<ApprovalRequest>> ListAsync(
        ApprovalStatus? status, CancellationToken cancellationToken) =>
        await dbContext.Approvals
            .Where(a => status == null || a.Status == status)
            .OrderByDescending(a => a.RequestedAt)
            .ToListAsync(cancellationToken);

    public Task<bool> ExistsAsync(
        ClientId requester,
        ToolName toolName,
        ToolVersionNumber version,
        ApprovalStatus status,
        CancellationToken cancellationToken) =>
        dbContext.Approvals.AnyAsync(
            a => a.RequesterClientId == requester
                && a.ToolName == toolName
                && a.Version == version
                && a.Status == status,
            cancellationToken);

    public Task AddAsync(ApprovalRequest approval, CancellationToken cancellationToken)
    {
        dbContext.Approvals.Add(approval);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
