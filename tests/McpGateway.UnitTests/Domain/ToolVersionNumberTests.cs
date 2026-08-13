using McpGateway.Domain;
using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Domain;

public class ToolVersionNumberTests
{
    [Theory]
    [InlineData("1.2", 1, 2, 0)]
    [InlineData("1.2.3", 1, 2, 3)]
    [InlineData("0.1", 0, 1, 0)]
    public void Create_parses_and_normalizes(string input, int major, int minor, int patch)
    {
        var version = ToolVersionNumber.Create(input);

        Assert.Equal((major, minor, patch), (version.Major, version.Minor, version.Patch));
        Assert.Equal($"{major}.{minor}.{patch}", version.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("1")]
    [InlineData("1.2.3.4")]
    [InlineData("a.b")]
    [InlineData("-1.0")]
    [InlineData("1.-2")]
    [InlineData("1.2.-3")]
    public void Create_rejects_invalid_versions(string input)
    {
        Assert.Throws<DomainRuleException>(() => ToolVersionNumber.Create(input));
    }

    [Fact]
    public void Versions_compare_by_major_then_minor_then_patch()
    {
        Assert.True(ToolVersionNumber.Create("2.0") > ToolVersionNumber.Create("1.9.9"));
        Assert.True(ToolVersionNumber.Create("1.10") > ToolVersionNumber.Create("1.9"));
        Assert.True(ToolVersionNumber.Create("1.2.1") > ToolVersionNumber.Create("1.2"));
        Assert.True(ToolVersionNumber.Create("1.2") <= ToolVersionNumber.Create("1.2.0"));
    }

    [Fact]
    public void Two_part_and_three_part_forms_of_same_version_are_equal()
    {
        Assert.Equal(ToolVersionNumber.Create("1.2"), ToolVersionNumber.Create("1.2.0"));
    }
}
