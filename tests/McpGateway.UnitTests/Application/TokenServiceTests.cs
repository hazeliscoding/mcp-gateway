using McpGateway.Application;
using McpGateway.Application.Identities;
using McpGateway.Domain.Identities;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpGateway.UnitTests.Application;

public class TokenServiceTests
{
    private readonly FakeIdentityRepository _repository = new();
    private readonly FakeTokenIssuer _issuer = new();
    private readonly TokenService _service;

    public TokenServiceTests()
    {
        _service = new TokenService(
            _repository,
            new FakeSecretHasher(),
            _issuer,
            NullLogger<TokenService>.Instance);
    }

    private async Task<GatewayIdentity> SeedIdentityAsync(string clientId = "incident_agent", string secret = "s3cret")
    {
        var identity = GatewayIdentity.Register(
            ClientId.Create(clientId), IdentityType.Agent, "Incident Response Agent",
            new FakeSecretHasher().Hash(secret), ["queue.read"], DateTimeOffset.UtcNow);
        await _repository.AddAsync(identity, CancellationToken.None);
        return identity;
    }

    [Fact]
    public async Task Valid_credentials_issue_token_with_identity_claims()
    {
        await SeedIdentityAsync();

        var result = await _service.IssueTokenAsync("incident_agent", "s3cret", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("token-for:incident_agent", result.Value!.AccessToken);
        Assert.Equal("Bearer", result.Value.TokenType);
        Assert.Equal(900, result.Value.ExpiresIn);
        Assert.Equal(IdentityType.Agent, _issuer.LastSubject!.Type);
        Assert.Equal(["queue.read"], _issuer.LastSubject.Scopes);
    }

    [Fact]
    public async Task Unknown_client_wrong_secret_and_disabled_identity_fail_identically()
    {
        var identity = await SeedIdentityAsync();

        var unknown = await _service.IssueTokenAsync("ghost_agent", "s3cret", CancellationToken.None);
        var wrongSecret = await _service.IssueTokenAsync("incident_agent", "wrong", CancellationToken.None);

        identity.Disable();
        var disabled = await _service.IssueTokenAsync("incident_agent", "s3cret", CancellationToken.None);

        foreach (var result in new[] { unknown, wrongSecret, disabled })
        {
            Assert.Equal(OperationError.Validation, result.Error);
            Assert.Equal(unknown.Message, result.Message);
        }
    }

    [Fact]
    public async Task Malformed_client_id_fails_with_same_message_as_wrong_credentials()
    {
        var malformed = await _service.IssueTokenAsync("NOT-VALID!", "whatever", CancellationToken.None);
        var unknown = await _service.IssueTokenAsync("ghost_agent", "whatever", CancellationToken.None);

        Assert.Equal(OperationError.Validation, malformed.Error);
        Assert.Equal(unknown.Message, malformed.Message);
    }
}
