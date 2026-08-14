using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpGateway.Application.Authorization;
using McpGateway.Domain.Authorization;

namespace McpGateway.IntegrationTests;

[Collection("postgres")]
public sealed class AuthorizationApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;

    public AuthorizationApiTests(PostgresFixture postgres)
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
    public async Task Permits_when_agent_holds_the_required_scope()
    {
        await RegisterTool("authz_permit_tool", ["queue.read", "queue.redrive"]);
        using var agent = await AgentClient("authz_permit_agent", ["queue.read", "queue.redrive"]);

        var decision = await Authorize(agent, "authz_permit_tool");

        Assert.True(decision.Permit);
        Assert.Equal("1.0.0", decision.Version);
        Assert.Equal(AuthorizationReasonCode.Permitted, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public async Task Denies_and_lists_missing_scope_when_agent_lacks_it()
    {
        await RegisterTool("authz_deny_tool", ["queue.read", "queue.redrive"]);
        using var agent = await AgentClient("authz_underscoped_agent", ["queue.read"]);

        var decision = await Authorize(agent, "authz_deny_tool");

        Assert.False(decision.Permit);
        var reason = Assert.Single(decision.Reasons);
        Assert.Equal(AuthorizationReasonCode.MissingScopes, reason.Code);
        Assert.Contains("queue.redrive", reason.Message);
    }

    [Fact]
    public async Task Disabled_tool_is_denied_even_for_a_fully_scoped_agent()
    {
        await RegisterTool("authz_disabled_tool", ["queue.read"]);
        await _admin.PostAsync("/api/tools/authz_disabled_tool/disable", null);
        using var agent = await AgentClient("authz_disabled_agent", ["queue.read"]);

        var decision = await Authorize(agent, "authz_disabled_tool");

        Assert.False(decision.Permit);
        Assert.Equal(AuthorizationReasonCode.ToolDisabled, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public async Task Deprecated_version_cannot_be_invoked()
    {
        await RegisterTool("authz_deprecated_tool", ["queue.read"], version: "1.0");
        await AddVersion("authz_deprecated_tool", ["queue.read"], version: "2.0");
        await _admin.PostAsync("/api/tools/authz_deprecated_tool/versions/1.0/deprecate", null);
        using var agent = await AgentClient("authz_deprecated_agent", ["queue.read"]);

        var decision = await Authorize(agent, "authz_deprecated_tool", new { version = "1.0" });

        Assert.False(decision.Permit);
        Assert.Equal(AuthorizationReasonCode.VersionDeprecated, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public async Task Unregistered_tool_returns_404()
    {
        using var agent = await AgentClient("authz_missing_agent", ["queue.read"]);

        var response = await agent.PostAsJsonAsync("/api/tools/authz_absent_tool/authorize", new { });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Scopes_in_the_request_body_are_ignored_no_privilege_escalation()
    {
        await RegisterTool("authz_escalation_tool", ["queue.read", "queue.redrive"]);
        using var agent = await AgentClient("authz_escalation_agent", ["queue.read"]);

        // The agent tries to smuggle the missing scope through the body; it must be ignored.
        var response = await agent.PostAsJsonAsync("/api/tools/authz_escalation_tool/authorize", new
        {
            grantedScopes = new[] { "queue.read", "queue.redrive" },
            scopes = new[] { "queue.redrive" },
            requiredScopes = Array.Empty<string>(),
        });
        var decision = (await response.Content.ReadFromJsonAsync<AuthorizationDecisionResponse>(Json))!;

        Assert.False(decision.Permit);
        Assert.Equal(AuthorizationReasonCode.MissingScopes, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public async Task Authorize_requires_authentication()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/tools/anything/authorize", new { });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<AuthorizationDecisionResponse> Authorize(HttpClient client, string tool, object? body = null)
    {
        var response = await client.PostAsJsonAsync($"/api/tools/{tool}/authorize", body ?? new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthorizationDecisionResponse>(Json))!;
    }

    private async Task RegisterTool(string name, string[] requiredScopes, string version = "1.0")
    {
        var response = await _admin.PostAsJsonAsync("/api/tools", new
        {
            name,
            version,
            description = "Redrives messages from a dead-letter queue back to its source queue.",
            riskLevel = "Privileged",
            approvalRequired = true,
            requiredScopes,
            timeoutSeconds = 30,
            inputSchema = new { type = "object" },
            outputSchema = new { type = "object" },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task AddVersion(string name, string[] requiredScopes, string version)
    {
        var response = await _admin.PostAsJsonAsync($"/api/tools/{name}/versions", new
        {
            version,
            description = "Later version of the redrive tool.",
            riskLevel = "Privileged",
            approvalRequired = true,
            requiredScopes,
            timeoutSeconds = 30,
            inputSchema = new { type = "object" },
            outputSchema = new { type = "object" },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<HttpClient> AgentClient(string clientId, string[] grantedScopes)
    {
        var created = await _admin.PostAsJsonAsync("/api/identities", new
        {
            clientId,
            type = "Agent",
            displayName = "Authorization Test Agent",
            grantedScopes,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        var client = _factory.CreateClient();
        await GatewayApiFactory.AuthenticateAsync(client, clientId, secret);
        return client;
    }
}
