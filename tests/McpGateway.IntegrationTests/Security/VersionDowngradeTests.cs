using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpGateway.Application.Authorization;
using McpGateway.Domain.Authorization;

namespace McpGateway.IntegrationTests.Security;

/// <summary>
/// Attack class: an attacker names an older, deprecated version to dodge a control
/// tightened in a newer one — or hopes the "latest" resolution silently falls back
/// to a version that should no longer run.
/// </summary>
[Collection("postgres")]
public sealed class VersionDowngradeTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;

    public VersionDowngradeTests(PostgresFixture postgres)
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
    public async Task Latest_resolution_skips_a_deprecated_version()
    {
        var tool = $"downgrade_latest_{Guid.NewGuid():N}";
        await RegisterTool(tool, "1.0");
        await AddVersion(tool, "2.0");
        await Deprecate(tool, "1.0");
        using var agent = await AgentClient(["queue.read"]);

        // No explicit version: the gateway must resolve to the active 2.0, never the
        // deprecated 1.0 — otherwise a downgrade would happen silently.
        var decision = await Authorize(agent, tool, new { });

        Assert.Equal(AuthorizationOutcome.Permitted, decision.Outcome);
        Assert.Equal("2.0.0", decision.Version);
    }

    [Fact]
    public async Task Explicitly_requesting_a_deprecated_version_is_denied()
    {
        var tool = $"downgrade_explicit_{Guid.NewGuid():N}";
        await RegisterTool(tool, "1.0");
        await AddVersion(tool, "2.0");
        await Deprecate(tool, "1.0");
        using var agent = await AgentClient(["queue.read"]);

        var decision = await Authorize(agent, tool, new { version = "1.0" });

        Assert.Equal(AuthorizationOutcome.Denied, decision.Outcome);
        Assert.Equal(AuthorizationReasonCode.VersionDeprecated, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public async Task A_tool_whose_only_version_is_deprecated_cannot_be_invoked()
    {
        var tool = $"downgrade_allgone_{Guid.NewGuid():N}";
        await RegisterTool(tool, "1.0");
        await Deprecate(tool, "1.0");
        using var agent = await AgentClient(["queue.read"]);

        var decision = await Authorize(agent, tool, new { });

        Assert.Equal(AuthorizationOutcome.Denied, decision.Outcome);
    }

    private async Task<AuthorizationDecisionResponse> Authorize(HttpClient client, string tool, object body)
    {
        var response = await client.PostAsJsonAsync($"/api/tools/{tool}/authorize", body);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthorizationDecisionResponse>(Json))!;
    }

    private async Task RegisterTool(string name, string version)
    {
        var response = await _admin.PostAsJsonAsync("/api/tools", new
        {
            name,
            version,
            description = "Read-only lookup.",
            riskLevel = "ReadOnly",
            approvalRequired = false,
            requiredScopes = new[] { "queue.read" },
            timeoutSeconds = 30,
            inputSchema = new { type = "object" },
            outputSchema = new { type = "object" },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task AddVersion(string name, string version)
    {
        var response = await _admin.PostAsJsonAsync($"/api/tools/{name}/versions", new
        {
            version,
            description = "Read-only lookup.",
            riskLevel = "ReadOnly",
            approvalRequired = false,
            requiredScopes = new[] { "queue.read" },
            timeoutSeconds = 30,
            inputSchema = new { type = "object" },
            outputSchema = new { type = "object" },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task Deprecate(string name, string version)
    {
        var response = await _admin.PostAsync($"/api/tools/{name}/versions/{version}/deprecate", null);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<HttpClient> AgentClient(string[] scopes)
    {
        var clientId = $"downgrade_agent_{Guid.NewGuid():N}";
        var created = await _admin.PostAsJsonAsync("/api/identities", new
        {
            clientId,
            type = "Agent",
            displayName = "Downgrade Test Agent",
            grantedScopes = scopes,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        var client = _factory.CreateClient();
        await GatewayApiFactory.AuthenticateAsync(client, clientId, secret);
        return client;
    }
}
