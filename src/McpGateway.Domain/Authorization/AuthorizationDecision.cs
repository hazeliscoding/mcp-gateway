namespace McpGateway.Domain.Authorization;

/// <summary>One rule outcome contributing to a decision.</summary>
/// <param name="Code">Machine-readable reason.</param>
/// <param name="Message">Human-readable explanation (safe to surface to callers).</param>
public sealed record AuthorizationReason(AuthorizationReasonCode Code, string Message);

/// <summary>
/// Result of evaluating an <see cref="AuthorizationRequest"/>. A permit carries a
/// single <see cref="AuthorizationReasonCode.Permitted"/> reason; a deny carries
/// the reasons it failed on. The decision is data — enforcement (returning 403,
/// requiring approval, writing audit) is the caller's job in later phases.
/// </summary>
public sealed record AuthorizationDecision
{
    public bool Permit { get; }
    public IReadOnlyList<AuthorizationReason> Reasons { get; }

    private AuthorizationDecision(bool permit, IReadOnlyList<AuthorizationReason> reasons)
    {
        Permit = permit;
        Reasons = reasons;
    }

    public static AuthorizationDecision Permitted() =>
        new(true, [new AuthorizationReason(AuthorizationReasonCode.Permitted, "Authorized.")]);

    public static AuthorizationDecision Denied(AuthorizationReasonCode code, string message) =>
        new(false, [new AuthorizationReason(code, message)]);
}
