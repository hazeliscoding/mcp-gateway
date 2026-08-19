using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using McpGateway.Application.Authorization;
using McpGateway.Domain.Authorization;

namespace McpGateway.IntegrationTests.Security;

/// <summary>
/// Attack class: a low-privilege caller tries to gain rights it was not granted —
/// by injecting claims through the request body, forging or replaying a token, or
/// reaching operator-only endpoints.
/// </summary>
[Collection("postgres")]
public sealed class PrivilegeEscalationTests : IAsyncLifetime
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    // Unique per test-method instance: the shared Postgres persists across them.
    private readonly string _tool = $"escalation_target_{Guid.NewGuid():N}";
    private readonly GatewayApiFactory _factory;
    private readonly HttpClient _admin;
    private HttpClient _agent = null!;

    public PrivilegeEscalationTests(PostgresFixture postgres)
    {
        _factory = new GatewayApiFactory(postgres.ConnectionString);
        _admin = _factory.CreateClient();
    }

    public async Task InitializeAsync()
    {
        await GatewayApiFactory.AuthenticateAsync(_admin);

        // A privileged tool the agent lacks the scope for.
        var registered = await _admin.PostAsJsonAsync("/api/tools", new
        {
            name = _tool,
            version = "1.0",
            description = "Privileged action.",
            riskLevel = "Privileged",
            approvalRequired = false,
            requiredScopes = new[] { "queue.redrive" },
            timeoutSeconds = 30,
            inputSchema = new { type = "object" },
            outputSchema = new { type = "object" },
        });
        registered.EnsureSuccessStatusCode();

        var clientId = $"escalation_agent_{Guid.NewGuid():N}";
        var created = await _admin.PostAsJsonAsync("/api/identities", new
        {
            clientId,
            type = "Agent",
            displayName = "Escalation Test Agent",
            grantedScopes = new[] { "queue.read" },
        });
        created.EnsureSuccessStatusCode();
        var secret = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientSecret").GetString()!;

        _agent = _factory.CreateClient();
        await GatewayApiFactory.AuthenticateAsync(_agent, clientId, secret);
    }

    public async Task DisposeAsync()
    {
        _agent.Dispose();
        _admin.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Scopes_injected_through_the_request_body_do_not_widen_the_grant()
    {
        // The agent holds only queue.read; it smuggles extra scopes into the body.
        // Caller scopes come only from the signed token, so this must still be denied.
        var response = await _agent.PostAsJsonAsync($"/api/tools/{_tool}/authorize", new
        {
            action = "Invoke",
            scope = "queue.read queue.redrive gateway.admin",
            grantedScopes = new[] { "queue.redrive" },
            requiredScopes = Array.Empty<string>(),
        });

        var decision = (await response.Content.ReadFromJsonAsync<AuthorizationDecisionResponse>(Json))!;
        Assert.Equal(AuthorizationOutcome.Denied, decision.Outcome);
        Assert.Contains(decision.Reasons, r => r.Code == AuthorizationReasonCode.MissingScopes);
    }

    [Fact]
    public async Task Agent_cannot_grant_itself_the_admin_scope()
    {
        // Registering a new identity (with gateway.admin) is itself operator-only.
        var response = await _agent.PostAsJsonAsync("/api/identities", new
        {
            clientId = "self_promoted_admin",
            type = "Service",
            displayName = "Self-promoted",
            grantedScopes = new[] { "gateway.admin" },
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Token_forged_with_the_wrong_signing_key_is_rejected()
    {
        var forged = AttackTokens.Forge(AttackTokens.WrongSigningKey, DateTimeOffset.UtcNow.AddMinutes(15));
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", forged);

        var response = await client.GetAsync("/api/identities");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Expired_token_is_rejected()
    {
        // Correctly signed, but expired well beyond the 30s clock skew.
        var expired = AttackTokens.Forge(GatewayApiFactory.SigningKey, DateTimeOffset.UtcNow.AddMinutes(-60));
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);

        var response = await client.GetAsync("/api/identities");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Tampered_token_payload_is_rejected()
    {
        // Start from a genuine admin token, then alter a claim byte — the HMAC breaks.
        var valid = await GatewayApiFactory.AuthenticateAsync(_factory.CreateClient());
        var tampered = AttackTokens.Tamper(valid);
        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tampered);

        var response = await client.GetAsync("/api/identities");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Agent_cannot_decide_approvals_even_for_a_valid_request()
    {
        // Distinct from four-eyes: deciding is operator-only, so an agent is blocked
        // by scope before the four-eyes rule is ever reached.
        var response = await _agent.PostAsJsonAsync(
            "/api/approvals/00000000-0000-0000-0000-000000000001/approve", new { });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
