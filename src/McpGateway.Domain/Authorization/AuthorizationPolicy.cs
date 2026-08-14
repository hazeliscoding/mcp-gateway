using McpGateway.Domain.Tools;

namespace McpGateway.Domain.Authorization;

/// <summary>
/// The gateway's authorization engine: a pure, deterministic function from an
/// <see cref="AuthorizationRequest"/> to an <see cref="AuthorizationDecision"/>.
/// It owns the Phase 3 rules (kill switch, version lifecycle, scope coverage);
/// risk-based approval gating arrives in Phase 4 as an additional rule here, not
/// as logic scattered into controllers or prompts.
/// </summary>
public static class AuthorizationPolicy
{
    /// <summary>
    /// Evaluates the request against every rule in precedence order and returns
    /// the first denial, or a permit when all rules pass. No side effects.
    /// </summary>
    public static AuthorizationDecision Evaluate(AuthorizationRequest request)
    {
        // 1. Kill switch first: a disabled tool is closed to everyone, no matter
        //    which version or scopes are involved.
        if (!request.ToolEnabled)
        {
            return AuthorizationDecision.Denied(
                AuthorizationReasonCode.ToolDisabled,
                $"Tool '{request.ToolName}' is disabled.");
        }

        // 2. The action must target an existing version.
        if (request.Version is null)
        {
            return AuthorizationDecision.Denied(
                AuthorizationReasonCode.VersionNotFound,
                $"Tool '{request.ToolName}' has no matching version to {Describe(request.Action)}.");
        }

        // 3. Deprecated versions can still be discovered but never invoked.
        if (request.Action == ToolAction.Invoke && request.Version.Status == ToolVersionStatus.Deprecated)
        {
            return AuthorizationDecision.Denied(
                AuthorizationReasonCode.VersionDeprecated,
                $"Version {request.Version.Number} of '{request.ToolName}' is deprecated and cannot be invoked.");
        }

        // 4. The caller must hold every scope the version requires.
        var missing = MissingScopes(request.Version.RequiredScopes, request.GrantedScopes);
        if (missing.Count > 0)
        {
            return AuthorizationDecision.Denied(
                AuthorizationReasonCode.MissingScopes,
                $"Missing required scope(s): {string.Join(", ", missing)}.");
        }

        // Environment and Resource are carried on the request for audit and for
        // future rules; Phase 3 adds no environment-specific denial.
        return AuthorizationDecision.Permitted();
    }

    private static IReadOnlyList<string> MissingScopes(
        IReadOnlyList<string> required, IReadOnlyList<string> granted)
    {
        var held = new HashSet<string>(granted, StringComparer.Ordinal);
        return required.Where(scope => !held.Contains(scope)).ToList();
    }

    private static string Describe(ToolAction action) => action switch
    {
        ToolAction.Invoke => "invoke",
        ToolAction.Discover => "discover",
        _ => action.ToString().ToLowerInvariant(),
    };
}
