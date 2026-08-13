using McpGateway.Domain;
using McpGateway.Domain.Identities;

namespace McpGateway.UnitTests.Domain;

public class GatewayIdentityTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private static GatewayIdentity Register(
        string clientId = "incident_agent",
        IdentityType type = IdentityType.Agent,
        string displayName = "Incident Response Agent",
        string secretHash = "hashed-secret-value",
        IReadOnlyList<string>? scopes = null) =>
        GatewayIdentity.Register(
            ClientId.Create(clientId), type, displayName, secretHash, scopes ?? ["queue.read"], Now);

    [Fact]
    public void Register_creates_enabled_identity()
    {
        var identity = Register();

        Assert.True(identity.Enabled);
        Assert.Equal("incident_agent", identity.ClientId.Value);
        Assert.Equal(IdentityType.Agent, identity.Type);
        Assert.Equal(["queue.read"], identity.GrantedScopes);
        Assert.Equal(Now, identity.CreatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData("NotSnake")]
    [InlineData("2agent")]
    public void ClientId_rejects_invalid_values(string value)
    {
        Assert.Throws<DomainRuleException>(() => ClientId.Create(value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_rejects_missing_display_name(string displayName)
    {
        Assert.Throws<DomainRuleException>(() => Register(displayName: displayName));
    }

    [Fact]
    public void Register_rejects_display_name_over_100_characters()
    {
        Assert.Throws<DomainRuleException>(() => Register(displayName: new string('x', 101)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Register_rejects_missing_secret_hash(string hash)
    {
        Assert.Throws<DomainRuleException>(() => Register(secretHash: hash));
    }

    [Fact]
    public void Register_rejects_empty_or_invalid_scopes()
    {
        Assert.Throws<DomainRuleException>(() => Register(scopes: []));
        Assert.Throws<DomainRuleException>(() => Register(scopes: ["Queue.Read"]));
        Assert.Throws<DomainRuleException>(() => Register(scopes: ["queue.read", "queue.read"]));
    }

    [Fact]
    public void RotateSecret_replaces_hash_and_rejects_blank()
    {
        var identity = Register(secretHash: "old-hash");

        identity.RotateSecret("new-hash");

        Assert.Equal("new-hash", identity.SecretHash);
        Assert.Throws<DomainRuleException>(() => identity.RotateSecret(" "));
    }

    [Fact]
    public void Disable_and_enable_are_idempotent()
    {
        var identity = Register();

        identity.Disable();
        identity.Disable();
        Assert.False(identity.Enabled);

        identity.Enable();
        identity.Enable();
        Assert.True(identity.Enabled);
    }
}
