using McpGateway.Domain;
using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Domain;

public class ToolDefinitionTests
{
    [Fact]
    public void Register_creates_enabled_tool_with_single_active_version()
    {
        var tool = TestSpecs.RegisteredTool(version: "1.0");

        Assert.True(tool.Enabled);
        var version = Assert.Single(tool.Versions);
        Assert.Equal(ToolVersionStatus.Active, version.Status);
        Assert.Equal(ToolVersionNumber.Create("1.0"), version.Number);
        Assert.Equal(TestSpecs.Now, version.RegisteredAt);
    }

    [Fact]
    public void AddVersion_accepts_strictly_higher_version()
    {
        var tool = TestSpecs.RegisteredTool(version: "1.0");

        tool.AddVersion(TestSpecs.Valid(version: "1.1"), TestSpecs.Now);

        Assert.Equal(2, tool.Versions.Count);
        Assert.Equal(ToolVersionNumber.Create("1.1"), tool.LatestVersion.Number);
    }

    [Theory]
    [InlineData("1.0")]   // duplicate
    [InlineData("0.9")]   // lower
    [InlineData("1.0.0")] // duplicate via normalization
    public void AddVersion_rejects_versions_not_higher_than_latest(string version)
    {
        var tool = TestSpecs.RegisteredTool(version: "1.0");

        Assert.Throws<DomainRuleException>(() => tool.AddVersion(TestSpecs.Valid(version: version), TestSpecs.Now));
    }

    [Fact]
    public void Versions_are_exposed_in_ascending_order()
    {
        var tool = TestSpecs.RegisteredTool(version: "1.0");
        tool.AddVersion(TestSpecs.Valid(version: "1.2"), TestSpecs.Now);
        tool.AddVersion(TestSpecs.Valid(version: "2.0"), TestSpecs.Now);

        Assert.Equal(["1.0.0", "1.2.0", "2.0.0"], tool.Versions.Select(v => v.Number.ToString()).ToArray());
    }

    [Fact]
    public void DeprecateVersion_marks_version_and_is_idempotent()
    {
        var tool = TestSpecs.RegisteredTool(version: "1.0");
        var number = ToolVersionNumber.Create("1.0");

        tool.DeprecateVersion(number);
        tool.DeprecateVersion(number);

        Assert.Equal(ToolVersionStatus.Deprecated, tool.Versions.Single().Status);
    }

    [Fact]
    public void DeprecateVersion_rejects_unknown_version()
    {
        var tool = TestSpecs.RegisteredTool(version: "1.0");

        Assert.Throws<DomainRuleException>(() => tool.DeprecateVersion(ToolVersionNumber.Create("9.9")));
    }

    [Fact]
    public void Disable_and_enable_are_idempotent()
    {
        var tool = TestSpecs.RegisteredTool();

        tool.Disable();
        tool.Disable();
        Assert.False(tool.Enabled);

        tool.Enable();
        tool.Enable();
        Assert.True(tool.Enabled);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Register_rejects_missing_description(string description)
    {
        Assert.Throws<DomainRuleException>(() => TestSpecs.RegisteredToolWith(TestSpecs.Valid(description: description)));
    }

    [Fact]
    public void Register_rejects_description_over_500_characters()
    {
        Assert.Throws<DomainRuleException>(
            () => TestSpecs.RegisteredToolWith(TestSpecs.Valid(description: new string('x', 501))));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-5)]
    [InlineData(301)]
    public void Register_rejects_timeout_outside_1_to_300_seconds(int timeout)
    {
        Assert.Throws<DomainRuleException>(() => TestSpecs.RegisteredToolWith(TestSpecs.Valid(timeoutSeconds: timeout)));
    }

    [Fact]
    public void Register_rejects_empty_scopes()
    {
        Assert.Throws<DomainRuleException>(() => TestSpecs.RegisteredToolWith(TestSpecs.Valid(scopes: [])));
    }

    [Theory]
    [InlineData("Queue.Read")]
    [InlineData("queue read")]
    [InlineData("")]
    public void Register_rejects_malformed_scopes(string scope)
    {
        Assert.Throws<DomainRuleException>(() => TestSpecs.RegisteredToolWith(TestSpecs.Valid(scopes: [scope])));
    }

    [Fact]
    public void Register_rejects_duplicate_scopes()
    {
        Assert.Throws<DomainRuleException>(
            () => TestSpecs.RegisteredToolWith(TestSpecs.Valid(scopes: ["queue.read", "queue.read"])));
    }

    [Theory]
    [InlineData("not json at all")]
    [InlineData("[1,2,3]")] // valid JSON but not an object
    [InlineData("")]
    public void Register_rejects_invalid_schemas(string schema)
    {
        Assert.Throws<DomainRuleException>(() => TestSpecs.RegisteredToolWith(TestSpecs.Valid(inputSchema: schema)));
        Assert.Throws<DomainRuleException>(() => TestSpecs.RegisteredToolWith(TestSpecs.Valid(outputSchema: schema)));
    }
}
