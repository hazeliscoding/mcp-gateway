using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpGateway.Application.Approvals;
using McpGateway.Application.Authorization;
using McpGateway.Domain.Authorization;

namespace McpGateway.IntegrationTests.Security;

/// <summary>
/// Attack class: one caller tries to ride another caller's approval. An approval is a
/// standing grant bound to the (requester, tool, version) triple, so a second identity
/// must not inherit it — the closest thing this single-tenant gateway has to a
/// cross-tenant boundary.
/// </summary>
[Collection("postgres")]
public sealed class GrantIsolationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;

    public GrantIsolationTests(PostgresFixture postgres)
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
    public async Task One_agents_approval_does_not_permit_another_agent()
    {
        var tool = $"isolation_tool_{Guid.NewGuid():N}";
        await RegisterPrivilegedTool(tool);
        using var alice = await AgentClient(["queue.redrive"]);
        using var bob = await AgentClient(["queue.redrive"]);

        // Alice earns an approval and can invoke.
        var created = await alice.PostAsJsonAsync($"/api/tools/{tool}/approvals", new { });
        var approval = (await created.Content.ReadFromJsonAsync<ApprovalResponse>(Json))!;
        await _admin.PostAsJsonAsync($"/api/approvals/{approval.Id}/approve", new { note = "ok" });
        Assert.Equal(AuthorizationOutcome.Permitted, (await Authorize(alice, tool)).Outcome);

        // Bob holds the same scope but no approval of his own — he must still be gated.
        var bobDecision = await Authorize(bob, tool);

        Assert.Equal(AuthorizationOutcome.RequiresApproval, bobDecision.Outcome);
    }

    private async Task<AuthorizationDecisionResponse> Authorize(HttpClient client, string tool)
    {
        var response = await client.PostAsJsonAsync($"/api/tools/{tool}/authorize", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthorizationDecisionResponse>(Json))!;
    }

    private async Task RegisterPrivilegedTool(string name)
    {
        var response = await _admin.PostAsJsonAsync("/api/tools", new
        {
            name,
            version = "1.0",
            description = "Redrives a dead-letter queue.",
            riskLevel = "Privileged",
            approvalRequired = false,
            requiredScopes = new[] { "queue.redrive" },
            timeoutSeconds = 30,
            inputSchema = new { type = "object" },
            outputSchema = new { type = "object" },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<HttpClient> AgentClient(string[] scopes)
    {
        var clientId = $"isolation_agent_{Guid.NewGuid():N}";
        var created = await _admin.PostAsJsonAsync("/api/identities", new
        {
            clientId,
            type = "Agent",
            displayName = "Isolation Test Agent",
            grantedScopes = scopes,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        var client = _factory.CreateClient();
        await GatewayApiFactory.AuthenticateAsync(client, clientId, secret);
        return client;
    }
}
