using System.Text.Json;
using McpGateway.Domain.Tools;

namespace McpGateway.Application.Tools;

/// <summary>Registers a brand-new tool together with its first version.</summary>
public sealed record RegisterToolRequest(
    string Name,
    string Version,
    string Description,
    RiskLevel RiskLevel,
    bool ApprovalRequired,
    IReadOnlyList<string> RequiredScopes,
    int TimeoutSeconds,
    JsonElement InputSchema,
    JsonElement OutputSchema);

/// <summary>Registers a new (strictly higher) version of an existing tool.</summary>
public sealed record RegisterVersionRequest(
    string Version,
    string Description,
    RiskLevel RiskLevel,
    bool ApprovalRequired,
    IReadOnlyList<string> RequiredScopes,
    int TimeoutSeconds,
    JsonElement InputSchema,
    JsonElement OutputSchema);

/// <summary>Discovery filters; by default disabled tools are excluded.</summary>
public sealed record ToolDiscoveryFilter(
    RiskLevel? RiskLevel = null,
    bool IncludeDisabled = false,
    string? NameContains = null);
