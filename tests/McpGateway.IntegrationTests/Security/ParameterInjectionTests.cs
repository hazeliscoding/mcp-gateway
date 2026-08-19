using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace McpGateway.IntegrationTests.Security;

/// <summary>
/// Attack class: hostile strings in inputs — SQL/NoSQL injection payloads in query
/// filters, and oversized or malformed identifiers — must be treated as opaque data.
/// They should be rejected by validation or matched against nothing, never executed
/// and never crash the request.
/// </summary>
[Collection("postgres")]
public sealed class ParameterInjectionTests : IAsyncLifetime
{
    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;

    public ParameterInjectionTests(PostgresFixture postgres)
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

    [Theory]
    [InlineData("'; DROP TABLE audit_entries; --")]
    [InlineData("' OR '1'='1")]
    [InlineData("\" OR 1=1 --")]
    public async Task Injection_in_audit_filters_matches_nothing_and_does_not_execute(string payload)
    {
        // A malformed filter value cannot be a valid tool/actor identifier, so it
        // matches nothing rather than being interpolated into the query.
        var byTool = await _admin.GetAsync($"/api/audit?toolName={Uri.EscapeDataString(payload)}");
        var byActor = await _admin.GetAsync($"/api/audit?actor={Uri.EscapeDataString(payload)}");

        Assert.Equal(HttpStatusCode.OK, byTool.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byActor.StatusCode);
        Assert.Equal("[]", await byTool.Content.ReadAsStringAsync());
        Assert.Equal("[]", await byActor.Content.ReadAsStringAsync());

        // The table the payload tried to drop is still queryable.
        var sanity = await _admin.GetAsync("/api/audit?limit=1");
        Assert.Equal(HttpStatusCode.OK, sanity.StatusCode);
    }

    [Fact]
    public async Task Authorizing_a_malformed_tool_name_is_a_client_error_not_a_crash()
    {
        using var agent = await AgentClient();

        var response = await agent.PostAsJsonAsync(
            $"/api/tools/{Uri.EscapeDataString("not a valid name!!")}/authorize", new { });

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound,
            $"Expected a 4xx, got {(int)response.StatusCode}.");
    }

    [Fact]
    public async Task An_oversized_tool_name_is_rejected_by_validation()
    {
        var huge = new string('a', 5000);

        var response = await _admin.PostAsJsonAsync("/api/tools", new
        {
            name = huge,
            version = "1.0",
            description = "oversized",
            riskLevel = "ReadOnly",
            approvalRequired = false,
            requiredScopes = Array.Empty<string>(),
            timeoutSeconds = 30,
            inputSchema = new { type = "object" },
            outputSchema = new { type = "object" },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_hostile_resource_value_is_accepted_as_opaque_data_and_hashed()
    {
        var tool = $"inject_resource_{Guid.NewGuid():N}";
        await RegisterReadOnlyTool(tool);
        using var agent = await AgentClient();
        const string hostile = "<script>alert(1)</script>'; DROP TABLE tools; --";

        // The resource is opaque: the decision succeeds and the value is never echoed
        // back or stored raw (the audit trail hashes it).
        var decision = await agent.PostAsJsonAsync($"/api/tools/{tool}/authorize", new { resource = hostile });
        Assert.Equal(HttpStatusCode.OK, decision.StatusCode);

        var audit = await _admin.GetAsync($"/api/audit?toolName={tool}");
        var body = await audit.Content.ReadAsStringAsync();
        Assert.DoesNotContain("DROP TABLE", body);
        Assert.DoesNotContain("<script>", body);
    }

    private async Task RegisterReadOnlyTool(string name)
    {
        var response = await _admin.PostAsJsonAsync("/api/tools", new
        {
            name,
            version = "1.0",
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

    private async Task<HttpClient> AgentClient()
    {
        var clientId = $"inject_agent_{Guid.NewGuid():N}";
        var created = await _admin.PostAsJsonAsync("/api/identities", new
        {
            clientId,
            type = "Agent",
            displayName = "Injection Test Agent",
            grantedScopes = new[] { "queue.read" },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        var client = _factory.CreateClient();
        await GatewayApiFactory.AuthenticateAsync(client, clientId, secret);
        return client;
    }
}
