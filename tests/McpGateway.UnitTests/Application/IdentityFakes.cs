using McpGateway.Application.Identities;
using McpGateway.Domain.Identities;

namespace McpGateway.UnitTests.Application;

internal sealed class FakeIdentityRepository : IIdentityRepository
{
    private readonly Dictionary<string, GatewayIdentity> _identities = [];

    public Task<GatewayIdentity?> GetByClientIdAsync(ClientId clientId, CancellationToken cancellationToken) =>
        Task.FromResult(_identities.GetValueOrDefault(clientId.Value));

    public Task<IReadOnlyList<GatewayIdentity>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<GatewayIdentity>>(_identities.Values.ToList());

    public Task<bool> AnyAsync(CancellationToken cancellationToken) =>
        Task.FromResult(_identities.Count > 0);

    public Task AddAsync(GatewayIdentity identity, CancellationToken cancellationToken)
    {
        _identities.Add(identity.ClientId.Value, identity);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Reversible stand-in so tests can assert what was hashed without real key derivation.</summary>
internal sealed class FakeSecretHasher : ISecretHasher
{
    public string Hash(string secret) => $"hash:{secret}";

    public bool Verify(string secret, string storedHash) => storedHash == $"hash:{secret}";
}

internal sealed class FakeTokenIssuer : ITokenIssuer
{
    public TokenSubject? LastSubject { get; private set; }

    public IssuedToken Issue(TokenSubject subject)
    {
        LastSubject = subject;
        return new IssuedToken($"token-for:{subject.ClientId}", 900);
    }
}
