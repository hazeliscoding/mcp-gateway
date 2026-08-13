namespace McpGateway.Domain.Tools;

/// <summary>
/// Aggregate root for a registered tool. Owns all versions of the tool and
/// enforces registry invariants: unique monotonically increasing versions,
/// per-version deprecation, and a tool-level enable flag (the kill switch).
/// Timestamps are passed in by callers so the aggregate stays deterministic.
/// </summary>
public sealed class ToolDefinition
{
    public ToolName Name { get; private set; } = null!;

    /// <summary>Tool-level kill switch; disabled tools are hidden from discovery by default.</summary>
    public bool Enabled { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<ToolVersion> Versions => _versions.OrderBy(v => v.Number).ToList();

    /// <summary>Highest registered version regardless of status.</summary>
    public ToolVersion LatestVersion => _versions.MaxBy(v => v.Number)!;

    private readonly List<ToolVersion> _versions = [];

    private ToolDefinition()
    {
        // EF Core materialization only.
    }

    public static ToolDefinition Register(ToolName name, ToolVersionSpec firstVersion, DateTimeOffset utcNow)
    {
        var tool = new ToolDefinition
        {
            Name = name,
            Enabled = true,
            CreatedAt = utcNow,
        };
        tool._versions.Add(ToolVersion.Create(firstVersion, utcNow));
        return tool;
    }

    /// <exception cref="DomainConflictException">
    /// The new version is not strictly higher than the latest registered version
    /// (which also rejects duplicates).
    /// </exception>
    public ToolVersion AddVersion(ToolVersionSpec spec, DateTimeOffset utcNow)
    {
        if (spec.Number <= LatestVersion.Number)
        {
            throw new DomainConflictException(
                $"Version {spec.Number} must be higher than the latest registered version {LatestVersion.Number}.");
        }

        var version = ToolVersion.Create(spec, utcNow);
        _versions.Add(version);
        return version;
    }

    /// <summary>Idempotent per version.</summary>
    /// <exception cref="DomainRuleException">The version does not exist on this tool.</exception>
    public void DeprecateVersion(ToolVersionNumber number)
    {
        var version = _versions.FirstOrDefault(v => v.Number == number)
            ?? throw new DomainRuleException($"Tool '{Name}' has no version {number}.");
        version.Deprecate();
    }

    public void Enable() => Enabled = true;

    public void Disable() => Enabled = false;
}
