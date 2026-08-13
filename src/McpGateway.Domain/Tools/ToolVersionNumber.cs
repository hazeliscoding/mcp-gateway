namespace McpGateway.Domain.Tools;

/// <summary>
/// Tool version in <c>major.minor</c> or <c>major.minor.patch</c> form.
/// Input is normalized to three components ("1.2" becomes "1.2.0") so string
/// representations are stable when used as database keys.
/// </summary>
public sealed record ToolVersionNumber : IComparable<ToolVersionNumber>
{
    public int Major { get; }
    public int Minor { get; }
    public int Patch { get; }

    private ToolVersionNumber(int major, int minor, int patch)
    {
        Major = major;
        Minor = minor;
        Patch = patch;
    }

    /// <exception cref="DomainRuleException">The value is not a valid two- or three-part version.</exception>
    public static ToolVersionNumber Create(string value)
    {
        var parts = (value ?? string.Empty).Trim().Split('.');
        if (parts.Length is < 2 or > 3
            || !int.TryParse(parts[0], out var major)
            || !int.TryParse(parts[1], out var minor)
            || (parts.Length == 3 ? !int.TryParse(parts[2], out _) : false)
            || major < 0 || minor < 0)
        {
            throw new DomainRuleException(
                $"Version '{value}' is invalid: expected 'major.minor' or 'major.minor.patch' with non-negative integers.");
        }

        var patch = parts.Length == 3 ? int.Parse(parts[2]) : 0;
        if (patch < 0)
        {
            throw new DomainRuleException($"Version '{value}' is invalid: patch must be non-negative.");
        }

        return new ToolVersionNumber(major, minor, patch);
    }

    public int CompareTo(ToolVersionNumber? other) =>
        other is null ? 1 : (Major, Minor, Patch).CompareTo((other.Major, other.Minor, other.Patch));

    public static bool operator >(ToolVersionNumber left, ToolVersionNumber right) => left.CompareTo(right) > 0;
    public static bool operator <(ToolVersionNumber left, ToolVersionNumber right) => left.CompareTo(right) < 0;
    public static bool operator >=(ToolVersionNumber left, ToolVersionNumber right) => left.CompareTo(right) >= 0;
    public static bool operator <=(ToolVersionNumber left, ToolVersionNumber right) => left.CompareTo(right) <= 0;

    public override string ToString() => $"{Major}.{Minor}.{Patch}";
}
