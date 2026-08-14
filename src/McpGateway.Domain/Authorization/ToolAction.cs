namespace McpGateway.Domain.Authorization;

/// <summary>
/// Action a principal wants to perform against a tool. Phase 3 gates
/// <see cref="Invoke"/>; discovery is listed so the same engine can score
/// read-style access in later phases.
/// </summary>
public enum ToolAction
{
    Invoke,
    Discover,
}
