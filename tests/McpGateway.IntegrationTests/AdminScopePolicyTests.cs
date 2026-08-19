using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace McpGateway.IntegrationTests;

/// <summary>
/// Verifies the <c>gateway.admin</c> scope split between operator endpoints and
/// the endpoints agents use themselves (discovery, authorize, approval requests).
/// Policy failures short-circuit before the endpoint runs, so 403s are asserted
/// against nonexistent resources without setup.
/// </summary>
[Collection("postgres")]
public sealed class AdminScopePolicyTests : IAsyncLifetime
{
    // A fresh instance is created per test method, and the shared Postgres persists
    // across them, so the agent client id must be unique to avoid registration conflicts.
    private readonly string _agentClientId = $"scope_test_agent_{Guid.NewGuid():N}";
    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;
    private HttpClient _agent = null!;

    public AdminScopePolicyTests(PostgresFixture postgres)
    {
        _factory = new GatewayApiFactory(postgres.ConnectionString);
        _admin = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await GatewayApiFactory.AuthenticateAsync(_admin);

        var created = await _admin.PostAsJsonAsync("/api/identities", new
        {
            clientId = _agentClientId,
            type = "Agent",
            displayName = "Scope Policy Test Agent",
            grantedScopes = new[] { "queue.read" },
        });
        created.EnsureSuccessStatusCode();
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        _agent = _factory.CreateClient();
        await GatewayApiFactory.AuthenticateAsync(_agent, _agentClientId, secret);
    }

    public async Task DisposeAsync()
    {
        _agent.Dispose();
        _admin.Dispose();
        await _factory.DisposeAsync();
    }

    [Theory]
    [InlineData("GET", "/api/identities")]
    [InlineData("GET", "/api/identities/scope_test_agent")]
    [InlineData("POST", "/api/identities")]
    [InlineData("POST", "/api/identities/scope_test_agent/enable")]
    [InlineData("POST", "/api/identities/scope_test_agent/disable")]
    [InlineData("POST", "/api/identities/scope_test_agent/rotate-secret")]
    [InlineData("POST", "/api/tools")]
    [InlineData("POST", "/api/tools/some_tool/versions")]
    [InlineData("POST", "/api/tools/some_tool/enable")]
    [InlineData("POST", "/api/tools/some_tool/disable")]
    [InlineData("POST", "/api/tools/some_tool/versions/1.0/deprecate")]
    [InlineData("POST", "/api/approvals/00000000-0000-0000-0000-000000000001/approve")]
    [InlineData("POST", "/api/approvals/00000000-0000-0000-0000-000000000001/reject")]
    [InlineData("GET", "/api/audit")]
    [InlineData("GET", "/api/audit/stats")]
    public async Task Admin_endpoints_are_forbidden_without_the_admin_scope(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
        {
            request.Content = JsonContent.Create(new { });
        }

        var response = await _agent.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Agent_endpoints_stay_open_to_any_authenticated_caller()
    {
        const string tool = "scope_open_tool";
        var registered = await _admin.PostAsJsonAsync("/api/tools", new
        {
            name = tool,
            version = "1.0",
            description = "Read-only lookup.",
            riskLevel = "ReadOnly",
            approvalRequired = false,
            requiredScopes = new[] { "queue.read" },
            timeoutSeconds = 30,
            inputSchema = new { type = "object" },
            outputSchema = new { type = "object" },
        });
        Assert.Equal(HttpStatusCode.Created, registered.StatusCode);

        var discovery = await _agent.GetAsync("/api/tools");
        var detail = await _agent.GetAsync($"/api/tools/{tool}");
        var authorize = await _agent.PostAsJsonAsync($"/api/tools/{tool}/authorize", new { });
        var approvals = await _agent.GetAsync("/api/approvals");

        Assert.Equal(HttpStatusCode.OK, discovery.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal(HttpStatusCode.OK, authorize.StatusCode);
        Assert.Equal(HttpStatusCode.OK, approvals.StatusCode);
    }

    [Fact]
    public async Task Admin_scope_grants_access_to_admin_endpoints()
    {
        var identities = await _admin.GetAsync("/api/identities");

        Assert.Equal(HttpStatusCode.OK, identities.StatusCode);
    }
}
