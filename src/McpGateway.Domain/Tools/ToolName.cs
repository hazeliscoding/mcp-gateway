using System.Text.RegularExpressions;

namespace McpGateway.Domain.Tools;

/// <summary>
/// Unique tool identifier in snake_case (e.g. <c>redrive_dead_letter_queue</c>).
/// Format is restricted so names are safe to use as MCP tool names, URL
/// segments, and database keys without escaping.
/// </summary>
public sealed partial record ToolName
{
    /// <summary>Lowercase letter first, then lowercase letters/digits/underscores; 3–64 chars total.</summary>
    [GeneratedRegex("^[a-z][a-z0-9_]{2,63}$")]
    private static partial Regex Pattern();

    public string Value { get; }

    private ToolName(string value) => Value = value;

    /// <exception cref="DomainRuleException">The name does not match the required format.</exception>
    public static ToolName Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (!Pattern().IsMatch(trimmed))
        {
            throw new DomainRuleException(
                $"Tool name '{value}' is invalid: must be snake_case, start with a letter, and be 3-64 characters.");
        }

        return new ToolName(trimmed);
    }

    public override string ToString() => Value;
}
