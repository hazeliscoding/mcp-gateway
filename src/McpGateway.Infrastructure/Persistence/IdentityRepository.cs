using McpGateway.Application.Identities;
using McpGateway.Domain.Identities;
using Microsoft.EntityFrameworkCore;

namespace McpGateway.Infrastructure.Persistence;

public sealed class IdentityRepository(McpGatewayDbContext dbContext) : IIdentityRepository
{
    public async Task<GatewayIdentity?> GetByClientIdAsync(ClientId clientId, CancellationToken cancellationToken) =>
        await dbContext.Identities.FirstOrDefaultAsync(i => i.ClientId == clientId, cancellationToken);

    public async Task<IReadOnlyList<GatewayIdentity>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Identities.ToListAsync(cancellationToken);

    public Task<bool> AnyAsync(CancellationToken cancellationToken) =>
        dbContext.Identities.AnyAsync(cancellationToken);

    public Task AddAsync(GatewayIdentity identity, CancellationToken cancellationToken)
    {
        dbContext.Identities.Add(identity);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
