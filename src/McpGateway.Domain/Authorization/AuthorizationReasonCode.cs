namespace McpGateway.Domain.Authorization;

/// <summary>
/// Machine-readable outcome of a single authorization rule. Every deny carries
/// one of these so callers (and the Phase 5 audit trail) can act on the reason
/// without parsing free-text messages.
/// </summary>
public enum AuthorizationReasonCode
{
    /// <summary>All rules passed; the action is permitted.</summary>
    Permitted,

    /// <summary>The tool's kill switch is off — the whole tool is disabled.</summary>
    ToolDisabled,

    /// <summary>No matching version exists for the requested action.</summary>
    VersionNotFound,

    /// <summary>The target version is deprecated and cannot be invoked.</summary>
    VersionDeprecated,

    /// <summary>The principal is missing one or more required scopes.</summary>
    MissingScopes,

    /// <summary>The action is otherwise allowed but its risk class demands human approval.</summary>
    ApprovalRequired,

    /// <summary>The tool's risk class is barred from execution through the gateway.</summary>
    RiskProhibited,
}
