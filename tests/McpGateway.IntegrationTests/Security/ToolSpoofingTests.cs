using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace McpGateway.IntegrationTests.Security;

/// <summary>
/// Attack class: an attacker tries to impersonate or redefine a tool — overwriting an
/// existing registration, registering a look-alike without operator rights, or naming
/// a tool that was never registered.
/// </summary>
[Collection("postgres")]
public sealed class ToolSpoofingTests : IAsyncLifetime
{
    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;

    public ToolSpoofingTests(PostgresFixture postgres)
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
    public async Task Re_registering_an_existing_tool_name_is_rejected()
    {
        var tool = $"spoof_dup_{Guid.NewGuid():N}";
        Assert.Equal(HttpStatusCode.Created, (await Register(_admin, tool, "ReadOnly")).StatusCode);

        // A second registration under the same name must not silently replace the
        // trusted definition with an attacker's (e.g. relaxing the risk level).
        var second = await Register(_admin, tool, "Destructive");

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_non_operator_cannot_register_a_look_alike_tool()
    {
        using var agent = await AgentClient();

        var response = await Register(agent, $"spoof_lookalike_{Guid.NewGuid():N}", "ReadOnly");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Authorizing_a_tool_that_was_never_registered_is_not_found()
    {
        using var agent = await AgentClient();

        var response = await agent.PostAsJsonAsync($"/api/tools/spoof_ghost_{Guid.NewGuid():N}/authorize", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static Task<HttpResponseMessage> Register(HttpClient client, string name, string riskLevel) =>
        client.PostAsJsonAsync("/api/tools", new
        {
            name,
            version = "1.0",
            description = "Tool under test.",
            riskLevel,
            approvalRequired = false,
            requiredScopes = new[] { "queue.read" },
            timeoutSeconds = 30,
            inputSchema = new { type = "object" },
            outputSchema = new { type = "object" },
        });

    private async Task<HttpClient> AgentClient()
    {
        var clientId = $"spoof_agent_{Guid.NewGuid():N}";
        var created = await _admin.PostAsJsonAsync("/api/identities", new
        {
            clientId,
            type = "Agent",
            displayName = "Spoofing Test Agent",
            grantedScopes = new[] { "queue.read" },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        var client = _factory.CreateClient();
        await GatewayApiFactory.AuthenticateAsync(client, clientId, secret);
        return client;
    }
}
