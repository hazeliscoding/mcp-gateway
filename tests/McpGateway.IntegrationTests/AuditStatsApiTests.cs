using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpGateway.Application.Approvals;
using McpGateway.Application.Auditing;

namespace McpGateway.IntegrationTests;

/// <summary>
/// Exercises the audit stats aggregation against real activity. The Postgres
/// instance is shared across test classes, so assertions key on a uniquely-named
/// tool (deterministic counts) and on window filtering rather than global totals.
/// </summary>
[Collection("postgres")]
public sealed class AuditStatsApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;

    public AuditStatsApiTests(PostgresFixture postgres)
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
    public async Task Stats_aggregate_a_full_approval_loop_by_tool_and_outcome()
    {
        var tool = $"stats_loop_tool_{Guid.NewGuid():N}";
        await RegisterTool(tool, "Privileged", ["queue.redrive"]);
        using var agent = await AgentClient($"stats_loop_agent_{Guid.NewGuid():N}", ["queue.redrive"]);

        // Four audit events for this tool: RequiresApproval, ApprovalRequested,
        // ApprovalApproved, then Permitted.
        await Authorize(agent, tool);
        var created = await agent.PostAsJsonAsync($"/api/tools/{tool}/approvals", new { });
        var approval = (await created.Content.ReadFromJsonAsync<ApprovalResponse>(Json))!;
        await _admin.PostAsJsonAsync($"/api/approvals/{approval.Id}/approve", new { note = "ok" });
        await Authorize(agent, tool);

        var stats = await _admin.GetFromJsonAsync<AuditStatsResponse>("/api/audit/stats", Json);

        var toolCount = Assert.Single(stats!.EventsByTool, c => c.Name == tool);
        Assert.Equal(4, toolCount.Count);
        Assert.Contains(stats.AuthorizationOutcomes, c => c.Name == "RequiresApproval");
        Assert.Contains(stats.AuthorizationOutcomes, c => c.Name == "Permitted");
        Assert.Contains(stats.EventsByType, c => c.Name == "ApprovalApproved");
        Assert.True(stats.TotalEvents >= 4);
        Assert.Equal(stats.TotalEvents, stats.EventsPerDay.Sum(d => d.Count));
    }

    [Fact]
    public async Task Stats_window_excludes_activity_outside_the_range()
    {
        var tool = $"stats_window_tool_{Guid.NewGuid():N}";
        await RegisterTool(tool, "ReadOnly", ["queue.read"]);
        using var agent = await AgentClient($"stats_window_agent_{Guid.NewGuid():N}", ["queue.read"]);
        await Authorize(agent, tool);

        // A window that ends before today's activity should contain none of it.
        var stats = await _admin.GetFromJsonAsync<AuditStatsResponse>(
            "/api/audit/stats?from=2000-01-01T00:00:00Z&to=2000-01-02T00:00:00Z", Json);

        Assert.Equal(0, stats!.TotalEvents);
        Assert.DoesNotContain(stats.EventsByTool, c => c.Name == tool);
    }

    [Fact]
    public async Task Stats_reject_an_inverted_window()
    {
        var response = await _admin.GetAsync(
            "/api/audit/stats?from=2026-08-18T00:00:00Z&to=2026-08-17T00:00:00Z");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Stats_endpoint_requires_authentication()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/audit/stats");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task Authorize(HttpClient client, string tool)
    {
        var response = await client.PostAsJsonAsync($"/api/tools/{tool}/authorize", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task RegisterTool(string name, string riskLevel, string[] requiredScopes)
    {
        var response = await _admin.PostAsJsonAsync("/api/tools", new
        {
            name,
            version = "1.0",
            description = "Operates on a dead-letter queue.",
            riskLevel,
            approvalRequired = false,
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
            displayName = "Stats Test Agent",
            grantedScopes,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        var client = _factory.CreateClient();
        await GatewayApiFactory.AuthenticateAsync(client, clientId, secret);
        return client;
    }
}
