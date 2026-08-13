namespace McpGateway.Domain.Tools;

/// <summary>
/// Everything required to register one version of a tool. Validated by
/// <see cref="ToolVersion"/> when the aggregate accepts it.
/// </summary>
public sealed record ToolVersionSpec(
    ToolVersionNumber Number,
    string Description,
    RiskLevel RiskLevel,
    bool ApprovalRequired,
    IReadOnlyList<string> RequiredScopes,
    int TimeoutSeconds,
    string InputSchemaJson,
    string OutputSchemaJson);
