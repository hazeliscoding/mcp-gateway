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
        await SeedTool("redrive_dead_letter_queue", "1.0", "queue.read", "queue.redrive");

        var result = await Authorize("redrive_dead_letter_queue",
            caller: Caller("queue.read", "queue.redrive"));

        Assert.True(result.IsSuccess);
        Assert.True(result.Value!.Permit);
        Assert.Equal("1.0.0", result.Value.Version);
    }

    [Fact]
    public async Task Denies_and_surfaces_missing_scope_reason()
    {
        await SeedTool("redrive_dead_letter_queue", "1.0", "queue.read", "queue.redrive");

        var result = await Authorize("redrive_dead_letter_queue", caller: Caller("queue.read"));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Permit);
        var reason = Assert.Single(result.Value.Reasons);
        Assert.Equal(AuthorizationReasonCode.MissingScopes, reason.Code);
        Assert.Contains("queue.redrive", reason.Message);
    }

    [Fact]
    public async Task Resolves_the_latest_active_version_when_none_requested()
    {
        var tool = ToolDefinition.Register(
            ToolName.Create("redrive_dead_letter_queue"), Spec("1.0", "queue.read"), Now);
        tool.AddVersion(Spec("1.1", "queue.read"), Now);
        await _repository.AddAsync(tool, CancellationToken.None);

        var result = await Authorize("redrive_dead_letter_queue", caller: Caller("queue.read"));

        Assert.Equal("1.1.0", result.Value!.Version);
        Assert.True(result.Value.Permit);
    }

    [Fact]
    public async Task Honors_an_explicitly_requested_version()
    {
        var tool = ToolDefinition.Register(
            ToolName.Create("redrive_dead_letter_queue"), Spec("1.0", "queue.read"), Now);
        tool.AddVersion(Spec("2.0", "queue.read"), Now);
        await _repository.AddAsync(tool, CancellationToken.None);

        var result = await Authorize("redrive_dead_letter_queue",
            new AuthorizeToolRequest(Version: "1.0"), Caller("queue.read"));

        Assert.Equal("1.0.0", result.Value!.Version);
    }

    [Fact]
    public async Task Requesting_an_unknown_version_denies_with_version_not_found()
    {
        await SeedTool("redrive_dead_letter_queue", "1.0", "queue.read");

        var result = await Authorize("redrive_dead_letter_queue",
            new AuthorizeToolRequest(Version: "9.9"), Caller("queue.read"));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.Permit);
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

    private async Task SeedTool(string name, string version, params string[] scopes)
    {
        var tool = ToolDefinition.Register(ToolName.Create(name), Spec(version, scopes), Now);
        await _repository.AddAsync(tool, CancellationToken.None);
    }

    private static ToolVersionSpec Spec(string version, params string[] scopes) =>
        new(
            ToolVersionNumber.Create(version),
            "Redrives messages from a dead-letter queue back to its source queue.",
            RiskLevel.Privileged,
            ApprovalRequired: true,
            scopes,
            TimeoutSeconds: 30,
            """{"type":"object"}""",
            """{"type":"object"}""");

    private static CallerPrincipal Caller(params string[] scopes) =>
        new(ClientId.Create("incident_agent"), IdentityType.Agent, scopes);
}
