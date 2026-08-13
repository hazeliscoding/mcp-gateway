using System.Text.Json;
using McpGateway.Application;
using McpGateway.Application.Tools;
using McpGateway.Domain.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpGateway.UnitTests.Application;

public class ToolRegistryServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeToolRegistryRepository _repository = new();
    private readonly ToolRegistryService _service;

    public ToolRegistryServiceTests()
    {
        _service = new ToolRegistryService(
            _repository,
            new FixedTimeProvider(Now),
            NullLogger<ToolRegistryService>.Instance);
    }

    private static JsonElement Schema(string json = """{"type":"object"}""") =>
        JsonSerializer.Deserialize<JsonElement>(json);

    private static RegisterToolRequest Register(
        string name = "get_queue_metrics",
        string version = "1.0",
        RiskLevel riskLevel = RiskLevel.ReadOnly) =>
        new(name, version, "Returns queue depth and message age metrics.", riskLevel,
            ApprovalRequired: false, RequiredScopes: ["queue.read"], TimeoutSeconds: 10, Schema(), Schema());

    private static RegisterVersionRequest Version(string version) =>
        new(version, "Adds DLQ depth to the metrics payload.", RiskLevel.ReadOnly,
            ApprovalRequired: false, RequiredScopes: ["queue.read"], TimeoutSeconds: 10, Schema(), Schema());

    [Fact]
    public async Task RegisterTool_returns_detail_and_persists()
    {
        var result = await _service.RegisterToolAsync(Register(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("get_queue_metrics", result.Value!.Name);
        Assert.Equal("1.0.0", Assert.Single(result.Value.Versions).Version);
        Assert.Equal(1, _repository.SaveCount);
    }

    [Fact]
    public async Task RegisterTool_rejects_duplicate_name_as_conflict()
    {
        await _service.RegisterToolAsync(Register(), CancellationToken.None);

        var result = await _service.RegisterToolAsync(Register(), CancellationToken.None);

        Assert.Equal(OperationError.Conflict, result.Error);
    }

    [Theory]
    [InlineData("Not_Snake_Case")]
    [InlineData("x")]
    public async Task RegisterTool_rejects_invalid_name_as_validation_error(string name)
    {
        var result = await _service.RegisterToolAsync(Register(name: name), CancellationToken.None);

        Assert.Equal(OperationError.Validation, result.Error);
    }

    [Fact]
    public async Task RegisterTool_rejects_invalid_spec_without_persisting()
    {
        var request = Register() with { TimeoutSeconds = 0 };

        var result = await _service.RegisterToolAsync(request, CancellationToken.None);

        Assert.Equal(OperationError.Validation, result.Error);
        Assert.Equal(0, _repository.SaveCount);
    }

    [Fact]
    public async Task RegisterVersion_appends_higher_version()
    {
        await _service.RegisterToolAsync(Register(version: "1.0"), CancellationToken.None);

        var result = await _service.RegisterVersionAsync("get_queue_metrics", Version("1.1"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Versions.Count);
    }

    [Fact]
    public async Task RegisterVersion_maps_lower_version_to_conflict()
    {
        await _service.RegisterToolAsync(Register(version: "2.0"), CancellationToken.None);

        var result = await _service.RegisterVersionAsync("get_queue_metrics", Version("1.9"), CancellationToken.None);

        Assert.Equal(OperationError.Conflict, result.Error);
    }

    [Fact]
    public async Task RegisterVersion_for_unknown_tool_returns_not_found()
    {
        var result = await _service.RegisterVersionAsync("missing_tool", Version("1.0"), CancellationToken.None);

        Assert.Equal(OperationError.NotFound, result.Error);
    }

    [Fact]
    public async Task SetEnabled_toggles_and_persists()
    {
        await _service.RegisterToolAsync(Register(), CancellationToken.None);

        var disabled = await _service.SetEnabledAsync("get_queue_metrics", false, CancellationToken.None);
        var detail = await _service.GetToolAsync("get_queue_metrics", CancellationToken.None);

        Assert.True(disabled.IsSuccess);
        Assert.False(detail.Value!.Enabled);
    }

    [Fact]
    public async Task SetEnabled_for_unknown_tool_returns_not_found()
    {
        var result = await _service.SetEnabledAsync("missing_tool", true, CancellationToken.None);

        Assert.Equal(OperationError.NotFound, result.Error);
    }

    [Fact]
    public async Task DeprecateVersion_marks_version_deprecated()
    {
        await _service.RegisterToolAsync(Register(version: "1.0"), CancellationToken.None);

        var result = await _service.DeprecateVersionAsync("get_queue_metrics", "1.0", CancellationToken.None);
        var detail = await _service.GetToolAsync("get_queue_metrics", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ToolVersionStatus.Deprecated, Assert.Single(detail.Value!.Versions).Status);
    }

    [Fact]
    public async Task DeprecateVersion_for_unknown_version_returns_not_found()
    {
        await _service.RegisterToolAsync(Register(version: "1.0"), CancellationToken.None);

        var result = await _service.DeprecateVersionAsync("get_queue_metrics", "9.9", CancellationToken.None);

        Assert.Equal(OperationError.NotFound, result.Error);
    }

    [Fact]
    public async Task ListTools_excludes_disabled_by_default_but_can_include_them()
    {
        await _service.RegisterToolAsync(Register(name: "get_queue_metrics"), CancellationToken.None);
        await _service.RegisterToolAsync(Register(name: "restart_worker_service"), CancellationToken.None);
        await _service.SetEnabledAsync("restart_worker_service", false, CancellationToken.None);

        var visible = await _service.ListToolsAsync(new ToolDiscoveryFilter(), CancellationToken.None);
        var all = await _service.ListToolsAsync(new ToolDiscoveryFilter(IncludeDisabled: true), CancellationToken.None);

        Assert.Equal(["get_queue_metrics"], visible.Value!.Select(t => t.Name).ToArray());
        Assert.Equal(2, all.Value!.Count);
    }

    [Fact]
    public async Task ListTools_filters_by_latest_version_risk_level_and_name()
    {
        await _service.RegisterToolAsync(Register(name: "get_queue_metrics", riskLevel: RiskLevel.ReadOnly), CancellationToken.None);
        await _service.RegisterToolAsync(Register(name: "redrive_dead_letter_queue", riskLevel: RiskLevel.Privileged), CancellationToken.None);

        var privileged = await _service.ListToolsAsync(
            new ToolDiscoveryFilter(RiskLevel: RiskLevel.Privileged), CancellationToken.None);
        var byName = await _service.ListToolsAsync(
            new ToolDiscoveryFilter(NameContains: "queue_metrics"), CancellationToken.None);

        Assert.Equal(["redrive_dead_letter_queue"], privileged.Value!.Select(t => t.Name).ToArray());
        Assert.Equal(["get_queue_metrics"], byName.Value!.Select(t => t.Name).ToArray());
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
