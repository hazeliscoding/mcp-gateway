using McpGateway.Domain;
using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Domain;

public class ToolNameTests
{
    [Theory]
    [InlineData("get_queue_metrics")]
    [InlineData("abc")]
    [InlineData("tool_2")]
    public void Create_accepts_valid_snake_case_names(string value)
    {
        Assert.Equal(value, ToolName.Create(value).Value);
    }

    [Fact]
    public void Create_trims_surrounding_whitespace()
    {
        Assert.Equal("get_queue_metrics", ToolName.Create("  get_queue_metrics  ").Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("ab")]                  // too short
    [InlineData("GetQueueMetrics")]     // not snake_case
    [InlineData("2fast")]               // starts with digit
    [InlineData("_private")]            // starts with underscore
    [InlineData("has space")]
    [InlineData("has-dash")]
    public void Create_rejects_invalid_names(string value)
    {
        Assert.Throws<DomainRuleException>(() => ToolName.Create(value));
    }

    [Fact]
    public void Create_rejects_names_longer_than_64_characters()
    {
        Assert.Throws<DomainRuleException>(() => ToolName.Create("a" + new string('b', 64)));
    }

    [Fact]
    public void Names_with_same_value_are_equal()
    {
        Assert.Equal(ToolName.Create("get_queue_metrics"), ToolName.Create("get_queue_metrics"));
    }
}
