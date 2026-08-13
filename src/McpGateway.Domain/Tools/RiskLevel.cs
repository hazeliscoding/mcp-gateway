namespace McpGateway.Domain.Tools;

/// <summary>
/// Risk classification of a tool version. The registry only stores this;
/// what each level is allowed to do is decided by the policy engine (Phase 4),
/// never by the registry or the model.
/// </summary>
public enum RiskLevel
{
    ReadOnly,
    Write,
    Privileged,
    Destructive,
}
