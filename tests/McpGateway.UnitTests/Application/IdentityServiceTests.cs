using McpGateway.Application;
using McpGateway.Application.Identities;
using McpGateway.Domain.Identities;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpGateway.UnitTests.Application;

public class IdentityServiceTests
{
    private readonly FakeIdentityRepository _repository = new();
    private readonly IdentityService _service;

    public IdentityServiceTests()
    {
        _service = new IdentityService(
            _repository,
            new FakeSecretHasher(),
            TimeProvider.System,
            NullLogger<IdentityService>.Instance);
    }

    private static RegisterIdentityRequest Register(string clientId = "incident_agent") =>
        new(clientId, IdentityType.Agent, "Incident Response Agent", ["queue.read", "queue.redrive"]);

    [Fact]
    public async Task Register_returns_secret_once_and_stores_only_hash()
    {
        var result = await _service.RegisterIdentityAsync(Register(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var secret = result.Value!.ClientSecret;
        Assert.False(string.IsNullOrWhiteSpace(secret));

        var stored = await _repository.GetByClientIdAsync(ClientId.Create("incident_agent"), CancellationToken.None);
        Assert.Equal($"hash:{secret}", stored!.SecretHash);

        // The identity projection never exposes the secret or its hash.
        var fetched = await _service.GetAsync("incident_agent", CancellationToken.None);
        Assert.IsType<IdentityResponse>(fetched.Value);
    }

    [Fact]
    public async Task Register_duplicate_client_id_is_conflict()
    {
        await _service.RegisterIdentityAsync(Register(), CancellationToken.None);

        var result = await _service.RegisterIdentityAsync(Register(), CancellationToken.None);

        Assert.Equal(OperationError.Conflict, result.Error);
    }

    [Theory]
    [InlineData("Bad_Client")]
    [InlineData("x")]
    public async Task Register_invalid_client_id_is_validation_error(string clientId)
    {
        var result = await _service.RegisterIdentityAsync(Register(clientId), CancellationToken.None);

        Assert.Equal(OperationError.Validation, result.Error);
    }

    [Fact]
    public async Task RotateSecret_returns_new_secret_and_invalidates_old_hash()
    {
        var registered = await _service.RegisterIdentityAsync(Register(), CancellationToken.None);
        var original = registered.Value!.ClientSecret;

        var rotated = await _service.RotateSecretAsync("incident_agent", CancellationToken.None);

        Assert.True(rotated.IsSuccess);
        Assert.NotEqual(original, rotated.Value!.ClientSecret);
        var stored = await _repository.GetByClientIdAsync(ClientId.Create("incident_agent"), CancellationToken.None);
        Assert.Equal($"hash:{rotated.Value.ClientSecret}", stored!.SecretHash);
    }

    [Fact]
    public async Task SetEnabled_toggles_identity()
    {
        await _service.RegisterIdentityAsync(Register(), CancellationToken.None);

        await _service.SetEnabledAsync("incident_agent", false, CancellationToken.None);
        var detail = await _service.GetAsync("incident_agent", CancellationToken.None);

        Assert.False(detail.Value!.Enabled);
    }

    [Fact]
    public async Task Unknown_identity_is_not_found()
    {
        var get = await _service.GetAsync("missing_identity", CancellationToken.None);
        var rotate = await _service.RotateSecretAsync("missing_identity", CancellationToken.None);

        Assert.Equal(OperationError.NotFound, get.Error);
        Assert.Equal(OperationError.NotFound, rotate.Error);
    }

    [Fact]
    public async Task List_orders_by_client_id()
    {
        await _service.RegisterIdentityAsync(Register("zeta_service"), CancellationToken.None);
        await _service.RegisterIdentityAsync(Register("alpha_agent"), CancellationToken.None);

        var list = await _service.ListAsync(CancellationToken.None);

        Assert.Equal(["alpha_agent", "zeta_service"], list.Value!.Select(i => i.ClientId).ToArray());
    }
}
