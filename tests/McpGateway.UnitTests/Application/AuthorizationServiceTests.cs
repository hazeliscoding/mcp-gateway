using McpGateway.Application;
using McpGateway.Application.Authorization;
using McpGateway.Domain.Authorization;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpGateway.UnitTests.Application;

public class AuthorizationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeToolRegistryRepository _repository = new();
    private readonly AuthorizationService _service;

    public AuthorizationServiceTests()
    {
        _service = new AuthorizationService(_repository, NullLogger<AuthorizationService>.Instance);
    }

    [Fact]
    public async Task Permits_when_caller_scopes_cover_latest_active_version()
    {
        await SeedTool("get_queue_metrics", "1.0", RiskLevel.ReadOnly, ["queue.read"]);

        var result = await Authorize("get_queue_metrics", caller: Caller("queue.read"));

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthorizationOutcome.Permitted, result.Value!.Outcome);
        Assert.True(result.Value.Permit);
        Assert.Equal("1.0.0", result.Value.Version);
    }

    [Fact]
    public async Task Denies_and_surfaces_missing_scope_reason()
    {
        await SeedTool("redrive_dead_letter_queue", "1.0", RiskLevel.ReadOnly, ["queue.read", "queue.redrive"]);

        var result = await Authorize("redrive_dead_letter_queue", caller: Caller("queue.read"));

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthorizationOutcome.Denied, result.Value!.Outcome);
        var reason = Assert.Single(result.Value.Reasons);
        Assert.Equal(AuthorizationReasonCode.MissingScopes, reason.Code);
        Assert.Contains("queue.redrive", reason.Message);
    }

    [Fact]
    public async Task Privileged_tool_requires_approval()
    {
        await SeedTool("redrive_dead_letter_queue", "1.0", RiskLevel.Privileged, ["queue.redrive"]);

        var result = await Authorize("redrive_dead_letter_queue", caller: Caller("queue.redrive"));

        Assert.Equal(AuthorizationOutcome.RequiresApproval, result.Value!.Outcome);
        Assert.False(result.Value.Permit);
        Assert.Equal(AuthorizationReasonCode.ApprovalRequired, Assert.Single(result.Value.Reasons).Code);
    }

    [Fact]
    public async Task Destructive_tool_is_prohibited()
    {
        await SeedTool("purge_queue", "1.0", RiskLevel.Destructive, ["queue.purge"]);

        var result = await Authorize("purge_queue", caller: Caller("queue.purge"));

        Assert.Equal(AuthorizationOutcome.Prohibited, result.Value!.Outcome);
        Assert.Equal(AuthorizationReasonCode.RiskProhibited, Assert.Single(result.Value.Reasons).Code);
    }

    [Fact]
    public async Task Resolves_the_latest_active_version_when_none_requested()
    {
        var tool = ToolDefinition.Register(
            ToolName.Create("get_queue_metrics"), Spec("1.0", RiskLevel.ReadOnly, "queue.read"), Now);
        tool.AddVersion(Spec("1.1", RiskLevel.ReadOnly, "queue.read"), Now);
        await _repository.AddAsync(tool, CancellationToken.None);

        var result = await Authorize("get_queue_metrics", caller: Caller("queue.read"));

        Assert.Equal("1.1.0", result.Value!.Version);
        Assert.True(result.Value.Permit);
    }

    [Fact]
    public async Task Honors_an_explicitly_requested_version()
    {
        var tool = ToolDefinition.Register(
            ToolName.Create("get_queue_metrics"), Spec("1.0", RiskLevel.ReadOnly, "queue.read"), Now);
        tool.AddVersion(Spec("2.0", RiskLevel.ReadOnly, "queue.read"), Now);
        await _repository.AddAsync(tool, CancellationToken.None);

        var result = await Authorize("get_queue_metrics",
            new AuthorizeToolRequest(Version: "1.0"), Caller("queue.read"));

        Assert.Equal("1.0.0", result.Value!.Version);
    }

    [Fact]
    public async Task Requesting_an_unknown_version_denies_with_version_not_found()
    {
        await SeedTool("get_queue_metrics", "1.0", RiskLevel.ReadOnly, ["queue.read"]);

        var result = await Authorize("get_queue_metrics",
            new AuthorizeToolRequest(Version: "9.9"), Caller("queue.read"));

        Assert.True(result.IsSuccess);
        Assert.Equal(AuthorizationOutcome.Denied, result.Value!.Outcome);
        Assert.Equal(AuthorizationReasonCode.VersionNotFound, Assert.Single(result.Value.Reasons).Code);
    }

    [Fact]
    public async Task Unregistered_tool_returns_not_found()
    {
        var result = await Authorize("missing_tool", caller: Caller("queue.read"));

        Assert.Equal(OperationError.NotFound, result.Error);
    }

    [Fact]
    public async Task Malformed_tool_name_returns_validation_error()
    {
        var result = await Authorize("Not_Snake_Case", caller: Caller("queue.read"));

        Assert.Equal(OperationError.Validation, result.Error);
    }

    private Task<OperationResult<AuthorizationDecisionResponse>> Authorize(
        string toolName, AuthorizeToolRequest? request = null, CallerPrincipal? caller = null) =>
        _service.AuthorizeToolAsync(
            toolName,
            request ?? new AuthorizeToolRequest(),
            caller ?? Caller("queue.read"),
            CancellationToken.None);

    private async Task SeedTool(string name, string version, RiskLevel riskLevel, params string[] scopes)
    {
        var tool = ToolDefinition.Register(ToolName.Create(name), Spec(version, riskLevel, scopes), Now);
        await _repository.AddAsync(tool, CancellationToken.None);
    }

    private static ToolVersionSpec Spec(string version, RiskLevel riskLevel, params string[] scopes) =>
        new(
            ToolVersionNumber.Create(version),
            "Redrives messages from a dead-letter queue back to its source queue.",
            riskLevel,
            ApprovalRequired: false,
            scopes,
            TimeoutSeconds: 30,
            """{"type":"object"}""",
            """{"type":"object"}""");

    private static CallerPrincipal Caller(params string[] scopes) =>
        new(ClientId.Create("incident_agent"), IdentityType.Agent, scopes);
}
