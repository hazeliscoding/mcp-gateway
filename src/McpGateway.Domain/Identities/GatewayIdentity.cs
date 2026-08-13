namespace McpGateway.Domain.Identities;

/// <summary>
/// Aggregate root for a principal that can authenticate against the gateway.
/// Holds only a secret hash — raw secrets never enter the domain, and are
/// never stored or logged anywhere in the system.
/// </summary>
public sealed class GatewayIdentity
{
    private const int MaxDisplayNameLength = 100;

    public ClientId ClientId { get; private set; } = null!;
    public IdentityType Type { get; private set; }
    public string DisplayName { get; private set; } = null!;

    /// <summary>Opaque hash produced by the application layer's secret hasher.</summary>
    public string SecretHash { get; private set; } = null!;

    public IReadOnlyList<string> GrantedScopes => _grantedScopes;

    /// <summary>Identity-level kill switch; disabled identities cannot obtain tokens.</summary>
    public bool Enabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private List<string> _grantedScopes = [];

    private GatewayIdentity()
    {
        // EF Core materialization only.
    }

    public static GatewayIdentity Register(
        ClientId clientId,
        IdentityType type,
        string displayName,
        string secretHash,
        IReadOnlyList<string> grantedScopes,
        DateTimeOffset utcNow)
    {
        var trimmedName = displayName?.Trim() ?? string.Empty;
        if (trimmedName.Length is 0 or > MaxDisplayNameLength)
        {
            throw new DomainRuleException($"Display name is required and must be at most {MaxDisplayNameLength} characters.");
        }

        return new GatewayIdentity
        {
            ClientId = clientId,
            Type = type,
            DisplayName = trimmedName,
            SecretHash = RequireHash(secretHash),
            _grantedScopes = Scope.CreateManyNormalized(grantedScopes),
            Enabled = true,
            CreatedAt = utcNow,
        };
    }

    /// <summary>Replaces the stored hash; the old secret stops working immediately.</summary>
    public void RotateSecret(string newSecretHash) => SecretHash = RequireHash(newSecretHash);

    public void Enable() => Enabled = true;

    public void Disable() => Enabled = false;

    private static string RequireHash(string secretHash) =>
        string.IsNullOrWhiteSpace(secretHash)
            ? throw new DomainRuleException("A secret hash is required.")
            : secretHash;
}
