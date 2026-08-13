using McpGateway.Application.Identities;
using McpGateway.Domain.Identities;
using McpGateway.Infrastructure.Security;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace McpGateway.IntegrationTests;

public class Pbkdf2SecretHasherTests
{
    private readonly Pbkdf2SecretHasher _hasher = new();

    [Fact]
    public void Hash_and_verify_round_trip()
    {
        var hash = _hasher.Hash("correct-horse-battery-staple");

        Assert.True(_hasher.Verify("correct-horse-battery-staple", hash));
        Assert.False(_hasher.Verify("wrong-secret", hash));
    }

    [Fact]
    public void Same_secret_produces_different_hashes_per_salt()
    {
        Assert.NotEqual(_hasher.Hash("secret"), _hasher.Hash("secret"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-valid-format")]
    [InlineData("pbkdf2-sha256.notanumber.c2FsdA==.a2V5")]
    [InlineData("pbkdf2-sha256.100000.!!!.a2V5")]
    public void Verify_rejects_malformed_stored_hashes(string storedHash)
    {
        Assert.False(_hasher.Verify("secret", storedHash));
    }
}

public class JwtTokenIssuerTests
{
    private static readonly AuthOptions Options = new()
    {
        SigningKey = "integration-test-signing-key-at-least-32-bytes!",
        Issuer = "mcp-gateway-tests",
        Audience = "mcp-gateway-tests",
        TokenLifetimeMinutes = 15,
    };

    [Fact]
    public async Task Issued_token_validates_and_carries_identity_claims()
    {
        var issuer = new JwtTokenIssuer(Microsoft.Extensions.Options.Options.Create(Options), TimeProvider.System);

        var issued = issuer.Issue(new TokenSubject("incident_agent", IdentityType.Agent, ["queue.read", "queue.redrive"]));

        Assert.Equal(15 * 60, issued.ExpiresInSeconds);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(issued.AccessToken, new TokenValidationParameters
        {
            ValidIssuer = Options.Issuer,
            ValidAudience = Options.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Options.SigningKey)),
        });

        Assert.True(result.IsValid);
        Assert.Equal("incident_agent", result.Claims["sub"]);
        Assert.Equal("Agent", result.Claims["identity_type"]);
        Assert.Equal("queue.read queue.redrive", result.Claims["scope"]);
    }

    [Fact]
    public void Short_signing_key_is_rejected_at_construction()
    {
        var options = Microsoft.Extensions.Options.Options.Create(new AuthOptions { SigningKey = "too-short" });

        Assert.Throws<InvalidOperationException>(() => new JwtTokenIssuer(options, TimeProvider.System));
    }
}
