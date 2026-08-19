using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace McpGateway.IntegrationTests.Security;

/// <summary>
/// Attack class: harvesting secrets. Client secrets must appear exactly once (at
/// issue), never on reads; stored PBKDF2 hashes must never be exposed; and token
/// failures must be uniform so an attacker cannot enumerate valid client ids.
/// </summary>
[Collection("postgres")]
public sealed class SecretLeakageTests : IAsyncLifetime
{
    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;

    public SecretLeakageTests(PostgresFixture postgres)
    {
        _factory = new GatewayApiFactory(postgres.ConnectionString);
        _admin = _factory.CreateClient();
    }

    public Task InitializeAsync() => GatewayApiFactory.AuthenticateAsync(_admin);

    public async Task DisposeAsync()
    {
        _admin.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Identity_reads_never_carry_the_secret_or_its_hash()
    {
        var clientId = $"leak_agent_{Guid.NewGuid():N}";
        var created = await _admin.PostAsJsonAsync("/api/identities", new
        {
            clientId,
            type = "Agent",
            displayName = "Leak Test Agent",
            grantedScopes = new[] { "queue.read" },
        });
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        var one = await (await _admin.GetAsync($"/api/identities/{clientId}")).Content.ReadAsStringAsync();
        var all = await (await _admin.GetAsync("/api/identities")).Content.ReadAsStringAsync();

        // The raw secret appeared only in the registration response.
        Assert.DoesNotContain(secret, one);
        Assert.DoesNotContain(secret, all);
        // And no hash material is projected onto the read model.
        Assert.DoesNotContain("hash", one, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", one, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Wrong_unknown_and_disabled_clients_fail_identically()
    {
        var clientId = $"uniform_agent_{Guid.NewGuid():N}";
        var created = await _admin.PostAsJsonAsync("/api/identities", new
        {
            clientId,
            type = "Agent",
            displayName = "Uniform Failure Agent",
            grantedScopes = new[] { "queue.read" },
        });
        created.EnsureSuccessStatusCode();

        var wrongSecret = await Token(clientId, "definitely-not-the-secret");
        var unknownClient = await Token($"ghost_{Guid.NewGuid():N}", "whatever");

        await _admin.PostAsync($"/api/identities/{clientId}/disable", null);
        var disabled = await Token(clientId, "definitely-not-the-secret");

        // Same status and same error code for every failure — no signal that
        // distinguishes "wrong secret" from "no such client" from "disabled".
        Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownClient.Status);
        Assert.Equal(HttpStatusCode.Unauthorized, disabled.Status);
        Assert.Equal("invalid_client", wrongSecret.Error);
        Assert.Equal("invalid_client", unknownClient.Error);
        Assert.Equal("invalid_client", disabled.Error);
    }

    private async Task<(HttpStatusCode Status, string? Error)> Token(string clientId, string secret)
    {
        var response = await _admin.PostAsync("/oauth/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        }));
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var error = body.TryGetProperty("error", out var e) ? e.GetString() : null;
        return (response.StatusCode, error);
    }
}
