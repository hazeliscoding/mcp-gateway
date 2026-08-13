using System.Text.Json;

namespace McpGateway.Domain.Tools;

/// <summary>
/// One registered version of a tool: its contract (input/output schemas),
/// risk metadata, and lifecycle status. Created only through
/// <see cref="ToolDefinition"/> so version invariants hold per tool.
/// </summary>
public sealed class ToolVersion
{
    private const int MaxDescriptionLength = 500;
    private const int MinTimeoutSeconds = 1;
    private const int MaxTimeoutSeconds = 300;

    public ToolVersionNumber Number { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public RiskLevel RiskLevel { get; private set; }
    public bool ApprovalRequired { get; private set; }
    public IReadOnlyList<string> RequiredScopes => _requiredScopes;
    public int TimeoutSeconds { get; private set; }
    public string InputSchemaJson { get; private set; } = null!;
    public string OutputSchemaJson { get; private set; } = null!;
    public ToolVersionStatus Status { get; private set; }
    public DateTimeOffset RegisteredAt { get; private set; }

    private List<string> _requiredScopes = [];

    private ToolVersion()
    {
        // EF Core materialization only.
    }

    internal static ToolVersion Create(ToolVersionSpec spec, DateTimeOffset utcNow)
    {
        var description = spec.Description?.Trim() ?? string.Empty;
        if (description.Length is 0 or > MaxDescriptionLength)
        {
            throw new DomainRuleException($"Description is required and must be at most {MaxDescriptionLength} characters.");
        }

        if (spec.TimeoutSeconds is < MinTimeoutSeconds or > MaxTimeoutSeconds)
        {
            throw new DomainRuleException($"Timeout must be between {MinTimeoutSeconds} and {MaxTimeoutSeconds} seconds.");
        }

        var scopes = Scope.CreateManyNormalized(spec.RequiredScopes);
        EnsureJsonObject(spec.InputSchemaJson, "inputSchema");
        EnsureJsonObject(spec.OutputSchemaJson, "outputSchema");

        return new ToolVersion
        {
            Number = spec.Number,
            Description = description,
            RiskLevel = spec.RiskLevel,
            ApprovalRequired = spec.ApprovalRequired,
            _requiredScopes = scopes,
            TimeoutSeconds = spec.TimeoutSeconds,
            InputSchemaJson = spec.InputSchemaJson,
            OutputSchemaJson = spec.OutputSchemaJson,
            Status = ToolVersionStatus.Active,
            RegisteredAt = utcNow,
        };
    }

    /// <summary>Idempotent: deprecating an already-deprecated version is a no-op.</summary>
    internal void Deprecate() => Status = ToolVersionStatus.Deprecated;

    private static void EnsureJsonObject(string json, string fieldName)
    {
        try
        {
            using var document = JsonDocument.Parse(json ?? string.Empty);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new DomainRuleException($"{fieldName} must be a JSON object.");
            }
        }
        catch (JsonException)
        {
            throw new DomainRuleException($"{fieldName} is not valid JSON.");
        }
    }
}
