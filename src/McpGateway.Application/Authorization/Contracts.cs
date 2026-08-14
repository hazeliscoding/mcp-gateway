using McpGateway.Domain.Authorization;

namespace McpGateway.Application.Authorization;

/// <summary>
/// What the caller is asking about: which tool version, doing what, where.
/// Deliberately carries no scopes — the caller's authority comes from its token,
/// never from this body.
/// </summary>
/// <param name="Version">Target version, or <see langword="null"/> for the latest active version.</param>
/// <param name="Action">Action being evaluated; defaults to invocation.</param>
/// <param name="Environment">Target environment; defaults to <c>production</c> when omitted.</param>
/// <param name="Resource">Optional resource identifier the action would touch.</param>
public sealed record AuthorizeToolRequest(
    string? Version = null,
    ToolAction Action = ToolAction.Invoke,
    string? Environment = null,
    string? Resource = null);

/// <summary>A single reason behind a decision.</summary>
public sealed record ReasonResponse(AuthorizationReasonCode Code, string Message);

/// <summary>The evaluated decision returned to the caller.</summary>
/// <param name="Permit">Whether the action is authorized.</param>
/// <param name="ToolName">Tool the decision concerns.</param>
/// <param name="Version">Resolved version, or the requested version when it could not be resolved.</param>
/// <param name="Action">Action that was evaluated.</param>
/// <param name="Reasons">Why the decision came out the way it did.</param>
public sealed record AuthorizationDecisionResponse(
    bool Permit,
    string ToolName,
    string? Version,
    ToolAction Action,
    IReadOnlyList<ReasonResponse> Reasons);
