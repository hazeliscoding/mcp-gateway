namespace McpGateway.Domain.Authorization;

/// <summary>
/// The classified result of an authorization decision. Richer than a boolean so
/// risk-based gating can distinguish "run it now" from "a human must approve" from
/// "categorically blocked".
/// </summary>
public enum AuthorizationOutcome
{
    /// <summary>Authorized to run automatically.</summary>
    Permitted,

    /// <summary>Otherwise authorized, but a human must approve before execution.</summary>
    RequiresApproval,

    /// <summary>Blocked by an access or lifecycle rule (missing scope, disabled, deprecated).</summary>
    Denied,

    /// <summary>Categorically not allowed through the gateway (e.g. destructive risk).</summary>
    Prohibited,
}
