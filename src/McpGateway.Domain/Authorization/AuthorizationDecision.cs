namespace McpGateway.Domain.Authorization;

/// <summary>One rule outcome contributing to a decision.</summary>
/// <param name="Code">Machine-readable reason.</param>
/// <param name="Message">Human-readable explanation (safe to surface to callers).</param>
public sealed record AuthorizationReason(AuthorizationReasonCode Code, string Message);

/// <summary>
/// Result of evaluating an <see cref="AuthorizationRequest"/>: a classified
/// <see cref="Outcome"/> plus the reasons behind it. The decision is data —
/// enforcement (returning 403, opening an approval, writing audit) is the caller's
/// job in later phases.
/// </summary>
public sealed record AuthorizationDecision
{
    public AuthorizationOutcome Outcome { get; }
    public IReadOnlyList<AuthorizationReason> Reasons { get; }

    /// <summary>Convenience for callers that only care whether the action may run automatically.</summary>
    public bool Permit => Outcome == AuthorizationOutcome.Permitted;

    private AuthorizationDecision(AuthorizationOutcome outcome, IReadOnlyList<AuthorizationReason> reasons)
    {
        Outcome = outcome;
        Reasons = reasons;
    }

    public static AuthorizationDecision Permitted() =>
        new(AuthorizationOutcome.Permitted,
            [new AuthorizationReason(AuthorizationReasonCode.Permitted, "Authorized.")]);

    public static AuthorizationDecision Denied(AuthorizationReasonCode code, string message) =>
        new(AuthorizationOutcome.Denied, [new AuthorizationReason(code, message)]);

    public static AuthorizationDecision RequiresApproval(string message) =>
        new(AuthorizationOutcome.RequiresApproval,
            [new AuthorizationReason(AuthorizationReasonCode.ApprovalRequired, message)]);

    public static AuthorizationDecision Prohibited(string message) =>
        new(AuthorizationOutcome.Prohibited,
            [new AuthorizationReason(AuthorizationReasonCode.RiskProhibited, message)]);
}
