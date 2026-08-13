using System.Text.RegularExpressions;

namespace McpGateway.Domain.Identities;

/// <summary>
/// Unique identity identifier in snake_case (e.g. <c>incident_agent</c>).
/// Same shape rules as tool names so identifiers stay safe in tokens, URLs,
/// and database keys.
/// </summary>
public sealed partial record ClientId
{
    [GeneratedRegex("^[a-z][a-z0-9_]{2,63}$")]
    private static partial Regex Pattern();

    public string Value { get; }

    private ClientId(string value) => Value = value;

    /// <exception cref="DomainRuleException">The client id does not match the required format.</exception>
    public static ClientId Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (!Pattern().IsMatch(trimmed))
        {
            throw new DomainRuleException(
                $"Client id '{value}' is invalid: must be snake_case, start with a letter, and be 3-64 characters.");
        }

        return new ClientId(trimmed);
    }

    public override string ToString() => Value;
}
