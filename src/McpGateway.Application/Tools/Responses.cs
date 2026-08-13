using System.Text.Json;
using McpGateway.Domain.Tools;

namespace McpGateway.Application.Tools;

/// <summary>Discovery list item: identity plus the latest version's headline metadata.</summary>
public sealed record ToolSummaryResponse(
    string Name,
    bool Enabled,
    string LatestVersion,
    string Description,
    RiskLevel RiskLevel,
    bool ApprovalRequired,
    DateTimeOffset CreatedAt);

/// <summary>Full tool detail including every registered version.</summary>
public sealed record ToolDetailResponse(
    string Name,
    bool Enabled,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ToolVersionResponse> Versions);

public sealed record ToolVersionResponse(
    string Version,
    string Description,
    RiskLevel RiskLevel,
    bool ApprovalRequired,
    IReadOnlyList<string> RequiredScopes,
    int TimeoutSeconds,
    JsonElement InputSchema,
    JsonElement OutputSchema,
    ToolVersionStatus Status,
    DateTimeOffset RegisteredAt);
