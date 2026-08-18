using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpGateway.Application.Approvals;
using McpGateway.Application.Authorization;
using McpGateway.Domain.Approvals;
using McpGateway.Domain.Authorization;

namespace McpGateway.IntegrationTests;

[Collection("postgres")]
public sealed class ApprovalApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;

    public ApprovalApiTests(PostgresFixture postgres)
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
    public async Task Full_loop_privileged_call_is_permitted_after_approval()
    {
        const string tool = "appr_loop_tool";
        await RegisterTool(tool, "Privileged", ["queue.redrive"]);
        using var agent = await AgentClient("appr_loop_agent", ["queue.redrive"]);

        // 1. Authorize is blocked pending approval.
        var blocked = await Authorize(agent, tool);
        Assert.Equal(AuthorizationOutcome.RequiresApproval, blocked.Outcome);

        // 2. The agent opens an approval request.
        var created = await agent.PostAsJsonAsync($"/api/tools/{tool}/approvals", new { });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var approval = (await created.Content.ReadFromJsonAsync<ApprovalResponse>(Json))!;
        Assert.Equal(ApprovalStatus.Pending, approval.Status);
        Assert.Equal("appr_loop_agent", approval.RequesterClientId);

        // 3. A different principal (the admin) approves it.
        var approved = await _admin.PostAsJsonAsync($"/api/approvals/{approval.Id}/approve", new { note = "ok" });
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        Assert.Equal(ApprovalStatus.Approved, (await approved.Content.ReadFromJsonAsync<ApprovalResponse>(Json))!.Status);

        // 4. Re-authorizing now permits the call.
        var permitted = await Authorize(agent, tool);
        Assert.Equal(AuthorizationOutcome.Permitted, permitted.Outcome);
        Assert.True(permitted.Permit);
    }

    [Fact]
    public async Task Requester_cannot_approve_its_own_request()
    {
        // Agents are blocked from deciding by the admin-scope policy (see
        // AdminScopePolicyTests); four-eyes must still hold for admins, so the
        // admin opens a request and then fails to approve it themselves.
        const string tool = "appr_selfapprove_tool";
        await RegisterTool(tool, "Privileged", ["queue.redrive"]);

        var created = await _admin.PostAsJsonAsync($"/api/tools/{tool}/approvals", new { });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var approval = (await created.Content.ReadFromJsonAsync<ApprovalResponse>(Json))!;

        var selfApprove = await _admin.PostAsJsonAsync($"/api/approvals/{approval.Id}/approve", new { });

        Assert.Equal(HttpStatusCode.BadRequest, selfApprove.StatusCode);
    }

    [Fact]
    public async Task Reject_transitions_the_request_and_leaves_authorization_blocked()
    {
        const string tool = "appr_reject_tool";
        await RegisterTool(tool, "Privileged", ["queue.redrive"]);
        using var agent = await AgentClient("appr_reject_agent", ["queue.redrive"]);

        var created = await agent.PostAsJsonAsync($"/api/tools/{tool}/approvals", new { });
        var approval = (await created.Content.ReadFromJsonAsync<ApprovalResponse>(Json))!;

        var rejected = await _admin.PostAsJsonAsync($"/api/approvals/{approval.Id}/reject", new { note = "too risky" });
        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal(ApprovalStatus.Rejected, (await rejected.Content.ReadFromJsonAsync<ApprovalResponse>(Json))!.Status);

        // A rejected request grants nothing.
        var stillBlocked = await Authorize(agent, tool);
        Assert.Equal(AuthorizationOutcome.RequiresApproval, stillBlocked.Outcome);
    }

    [Fact]
    public async Task Duplicate_pending_request_conflicts()
    {
        const string tool = "appr_duplicate_tool";
        await RegisterTool(tool, "Privileged", ["queue.redrive"]);
        using var agent = await AgentClient("appr_duplicate_agent", ["queue.redrive"]);

        await agent.PostAsJsonAsync($"/api/tools/{tool}/approvals", new { });
        var second = await agent.PostAsJsonAsync($"/api/tools/{tool}/approvals", new { });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Requesting_approval_for_an_automatic_tool_is_rejected()
    {
        const string tool = "appr_readonly_tool";
        await RegisterTool(tool, "ReadOnly", ["queue.read"]);
        using var agent = await AgentClient("appr_readonly_agent", ["queue.read"]);

        var response = await agent.PostAsJsonAsync($"/api/tools/{tool}/approvals", new { });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Pending_requests_are_listable_by_status()
    {
        const string tool = "appr_list_tool";
        await RegisterTool(tool, "Privileged", ["queue.redrive"]);
        using var agent = await AgentClient("appr_list_agent", ["queue.redrive"]);
        var created = await agent.PostAsJsonAsync($"/api/tools/{tool}/approvals", new { });
        var approval = (await created.Content.ReadFromJsonAsync<ApprovalResponse>(Json))!;

        var pending = await _admin.GetFromJsonAsync<List<ApprovalResponse>>("/api/approvals?status=Pending", Json);

        Assert.Contains(pending!, a => a.Id == approval.Id && a.ToolName == tool);
    }

    [Fact]
    public async Task Approval_endpoints_require_authentication()
    {
        using var anonymous = _factory.CreateClient();

        var request = await anonymous.PostAsJsonAsync("/api/tools/anything/approvals", new { });
        var list = await anonymous.GetAsync("/api/approvals");

        Assert.Equal(HttpStatusCode.Unauthorized, request.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }

    private async Task<AuthorizationDecisionResponse> Authorize(HttpClient client, string tool)
    {
        var response = await client.PostAsJsonAsync($"/api/tools/{tool}/authorize", new { });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthorizationDecisionResponse>(Json))!;
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
            displayName = "Approval Test Agent",
            grantedScopes,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        var client = _factory.CreateClient();
        await GatewayApiFactory.AuthenticateAsync(client, clientId, secret);
        return client;
    }
}
