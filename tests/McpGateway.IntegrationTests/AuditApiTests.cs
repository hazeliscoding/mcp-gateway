using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpGateway.Application.Approvals;
using McpGateway.Application.Auditing;
using McpGateway.Domain.Auditing;

namespace McpGateway.IntegrationTests;

[Collection("postgres")]
public sealed class AuditApiTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private const string SecretResource = "arn:aws:sqs:us-east-1:secret-queue";

    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;

    public AuditApiTests(PostgresFixture postgres)
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
    public async Task Approval_loop_is_captured_in_the_audit_trail()
    {
        const string tool = "audit_loop_tool";
        await RegisterTool(tool, "Privileged", ["queue.redrive"]);
        using var agent = await AgentClient("audit_loop_agent", ["queue.redrive"]);

        // Blocked attempt (with a sensitive resource), approval, then permitted.
        await Authorize(agent, tool, SecretResource);
        var created = await agent.PostAsJsonAsync($"/api/tools/{tool}/approvals", new { });
        var approval = (await created.Content.ReadFromJsonAsync<ApprovalResponse>(Json))!;
        await _admin.PostAsJsonAsync($"/api/approvals/{approval.Id}/approve", new { note = "ok" });
        await Authorize(agent, tool, SecretResource);

        var entries = await _admin.GetFromJsonAsync<List<AuditEntryResponse>>($"/api/audit?toolName={tool}", Json);

        var results = entries!.Select(e => (e.EventType, e.Result)).ToList();
        Assert.Contains((AuditEventType.AuthorizationDecision, "RequiresApproval"), results);
        Assert.Contains((AuditEventType.AuthorizationDecision, "Permitted"), results);
        Assert.Contains((AuditEventType.ApprovalRequested, "Requested"), results);
        Assert.Contains((AuditEventType.ApprovalApproved, "Approved"), results);
        Assert.All(entries!, e => Assert.False(string.IsNullOrWhiteSpace(e.TraceId)));
    }

    [Fact]
    public async Task Denied_authorization_is_recorded()
    {
        const string tool = "audit_deny_tool";
        await RegisterTool(tool, "ReadOnly", ["queue.read", "queue.redrive"]);
        using var agent = await AgentClient("audit_deny_agent", ["queue.read"]);

        await Authorize(agent, tool, resource: null);

        var entries = await _admin.GetFromJsonAsync<List<AuditEntryResponse>>(
            $"/api/audit?toolName={tool}&eventType=AuthorizationDecision", Json);

        Assert.Contains(entries!, e => e.Result == "Denied");
    }

    [Fact]
    public async Task Sensitive_request_context_is_hashed_not_stored_raw()
    {
        const string tool = "audit_redaction_tool";
        await RegisterTool(tool, "ReadOnly", ["queue.read"]);
        using var agent = await AgentClient("audit_redaction_agent", ["queue.read"]);
        await Authorize(agent, tool, SecretResource);

        var response = await _admin.GetAsync($"/api/audit?toolName={tool}");
        var body = await response.Content.ReadAsStringAsync();
        var entries = JsonSerializer.Deserialize<List<AuditEntryResponse>>(body, Json)!;

        Assert.DoesNotContain(SecretResource, body);
        Assert.All(entries, e => Assert.False(string.IsNullOrWhiteSpace(e.RequestHash)));
    }

    [Fact]
    public async Task Audit_endpoint_requires_authentication()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/audit");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task Authorize(HttpClient client, string tool, string? resource)
    {
        var response = await client.PostAsJsonAsync($"/api/tools/{tool}/authorize", new { resource });
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
            displayName = "Audit Test Agent",
            grantedScopes,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        var client = _factory.CreateClient();
        await GatewayApiFactory.AuthenticateAsync(client, clientId, secret);
        return client;
    }
}
