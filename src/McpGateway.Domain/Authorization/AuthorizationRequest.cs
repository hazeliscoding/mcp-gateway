using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;

namespace McpGateway.Domain.Authorization;

/// <summary>
/// The attributes evaluated for one authorization decision — the plan's ABAC
/// tuple of caller, tool, environment, resource, and action. The caller's
/// <see cref="GrantedScopes"/> come from the validated token upstream; the
/// engine never lets a caller assert its own scopes.
/// </summary>
/// <param name="CallerId">Authenticated principal making the request.</param>
/// <param name="CallerType">Kind of principal (user, agent, service).</param>
/// <param name="GrantedScopes">Scopes carried by the caller's token.</param>
/// <param name="ToolName">Target tool.</param>
/// <param name="ToolEnabled">Tool-level kill switch state.</param>
/// <param name="Version">
/// Resolved target version, or <see langword="null"/> when no matching version
/// exists — the engine denies that with <see cref="AuthorizationReasonCode.VersionNotFound"/>.
/// </param>
/// <param name="Action">What the caller wants to do.</param>
/// <param name="Environment">Deployment environment the call targets (e.g. <c>production</c>).</param>
/// <param name="Resource">Optional resource identifier the action would touch.</param>
/// <param name="ApprovalGranted">
/// Whether a human has already approved this caller for this tool version. Upgrades
/// an approval-gated outcome to <see cref="AuthorizationOutcome.Permitted"/>.
/// </param>
public sealed record AuthorizationRequest(
    ClientId CallerId,
    IdentityType CallerType,
    IReadOnlyList<string> GrantedScopes,
    ToolName ToolName,
    bool ToolEnabled,
    ToolVersion? Version,
    ToolAction Action,
    string Environment,
    string? Resource,
    bool ApprovalGranted = false);
