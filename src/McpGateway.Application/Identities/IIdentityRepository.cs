using McpGateway.Domain.Identities;

namespace McpGateway.Application.Identities;

/// <summary>Persistence boundary for gateway identities.</summary>
public interface IIdentityRepository
{
    Task<GatewayIdentity?> GetByClientIdAsync(ClientId clientId, CancellationToken cancellationToken);

    Task<IReadOnlyList<GatewayIdentity>> ListAsync(CancellationToken cancellationToken);

    Task<bool> AnyAsync(CancellationToken cancellationToken);

    Task AddAsync(GatewayIdentity identity, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
