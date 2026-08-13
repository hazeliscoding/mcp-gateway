using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace McpGateway.IntegrationTests;

[Collection("postgres")]
public sealed class AuthApiTests : IAsyncLifetime
{
    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;

    public AuthApiTests(PostgresFixture postgres)
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

    private static FormUrlEncodedContent TokenForm(
        string clientId, string secret, string grantType = "client_credentials") =>
        new(new Dictionary<string, string>
        {
            ["grant_type"] = grantType,
            ["client_id"] = clientId,
            ["client_secret"] = secret,
        });

    private static object AgentBody(string clientId) => new
    {
        clientId,
        type = "Agent",
        displayName = "Auth Test Agent",
        grantedScopes = new[] { "queue.read" },
    };

    [Fact]
    public async Task Registry_rejects_missing_and_garbage_tokens()
    {
        using var anonymous = _factory.CreateClient();

        var noToken = await anonymous.GetAsync("/api/tools");
        anonymous.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", "not.a.jwt");
        var badToken = await anonymous.GetAsync("/api/tools");

        Assert.Equal(HttpStatusCode.Unauthorized, noToken.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, badToken.StatusCode);
    }

    [Fact]
    public async Task Token_endpoint_rejects_wrong_secret_unknown_client_and_bad_grant()
    {
        using var client = _factory.CreateClient();

        var wrongSecret = await client.PostAsync("/oauth/token",
            TokenForm(GatewayApiFactory.BootstrapClientId, "wrong-secret"));
        var unknownClient = await client.PostAsync("/oauth/token",
            TokenForm("ghost_agent", "whatever"));
        var badGrant = await client.PostAsync("/oauth/token",
            TokenForm(GatewayApiFactory.BootstrapClientId, GatewayApiFactory.BootstrapSecret, grantType: "password"));
        var missingFields = await client.PostAsync("/oauth/token",
            new FormUrlEncodedContent(new Dictionary<string, string> { ["grant_type"] = "client_credentials" }));

        Assert.Equal(HttpStatusCode.Unauthorized, wrongSecret.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownClient.StatusCode);
        Assert.Equal("invalid_client", (await wrongSecret.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, badGrant.StatusCode);
        Assert.Equal("unsupported_grant_type", (await badGrant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("error").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, missingFields.StatusCode);
    }

    [Fact]
    public async Task Registered_agent_identity_can_authenticate_and_call_registry()
    {
        var created = await _admin.PostAsJsonAsync("/api/identities", AgentBody("auth_test_agent"));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var issued = await created.Content.ReadFromJsonAsync<JsonElement>();
        var secret = issued.GetProperty("clientSecret").GetString()!;
        Assert.False(string.IsNullOrWhiteSpace(secret));

        // Reads never expose the secret again.
        var fetched = await _admin.GetFromJsonAsync<JsonElement>("/api/identities/auth_test_agent");
        Assert.False(fetched.TryGetProperty("clientSecret", out _));
        Assert.False(fetched.TryGetProperty("secretHash", out _));

        using var agent = _factory.CreateClient();
        await GatewayApiFactory.AuthenticateAsync(agent, "auth_test_agent", secret);
        var registryCall = await agent.GetAsync("/api/tools");
        Assert.Equal(HttpStatusCode.OK, registryCall.StatusCode);
    }

    [Fact]
    public async Task Disabled_identity_is_refused_a_token()
    {
        var created = await _admin.PostAsJsonAsync("/api/identities", AgentBody("auth_disabled_agent"));
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        var disable = await _admin.PostAsync("/api/identities/auth_disabled_agent/disable", null);
        Assert.Equal(HttpStatusCode.NoContent, disable.StatusCode);

        using var client = _factory.CreateClient();
        var refused = await client.PostAsync("/oauth/token", TokenForm("auth_disabled_agent", secret));
        Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode);
    }

    [Fact]
    public async Task Rotated_secret_invalidates_the_old_one()
    {
        var created = await _admin.PostAsJsonAsync("/api/identities", AgentBody("auth_rotating_agent"));
        var original = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        var rotated = await _admin.PostAsync("/api/identities/auth_rotating_agent/rotate-secret", null);
        Assert.Equal(HttpStatusCode.OK, rotated.StatusCode);
        var replacement = (await rotated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;
        Assert.NotEqual(original, replacement);

        using var client = _factory.CreateClient();
        var oldSecret = await client.PostAsync("/oauth/token", TokenForm("auth_rotating_agent", original));
        var newSecret = await client.PostAsync("/oauth/token", TokenForm("auth_rotating_agent", replacement));
        Assert.Equal(HttpStatusCode.Unauthorized, oldSecret.StatusCode);
        Assert.Equal(HttpStatusCode.OK, newSecret.StatusCode);
    }
}
