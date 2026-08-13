using System.Text.RegularExpressions;

namespace McpGateway.Domain;

/// <summary>
/// Permission scope in dot-separated lowercase form (e.g. <c>queue.read</c>).
/// Shared by tool requirements and identity grants so both sides of the
/// authorization model (Phase 3+) speak the same vocabulary.
/// </summary>
public sealed partial record Scope
{
    [GeneratedRegex("^[a-z][a-z0-9_]*(\\.[a-z][a-z0-9_]*)*$")]
    private static partial Regex Pattern();

    public string Value { get; }

    private Scope(string value) => Value = value;

    /// <exception cref="DomainRuleException">The value does not match the scope format.</exception>
    public static Scope Create(string value)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (!Pattern().IsMatch(trimmed))
        {
            throw new DomainRuleException(
                $"Scope '{value}' is invalid: expected lowercase dot-separated segments like 'queue.read'.");
        }

        return new Scope(trimmed);
    }

    /// <summary>
    /// Validates and normalizes a scope list: at least one entry, valid format,
    /// no duplicates. Returns plain strings because aggregates persist scopes
    /// as string collections.
    /// </summary>
    /// <exception cref="DomainRuleException">The list is empty, malformed, or contains duplicates.</exception>
    public static List<string> CreateManyNormalized(IReadOnlyList<string> values)
    {
        if (values is null || values.Count == 0)
        {
            throw new DomainRuleException("At least one scope must be specified.");
        }

        var normalized = values.Select(v => Create(v).Value).ToList();
        if (normalized.Distinct().Count() != normalized.Count)
        {
            throw new DomainRuleException("Scopes must not contain duplicates.");
        }

        return normalized;
    }

    public override string ToString() => Value;
}
