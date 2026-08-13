namespace McpGateway.Domain.Identities;

/// <summary>
/// Kind of principal calling the gateway. Users are humans (admin console),
/// agents are AI agents, services are internal machine identities. Later
/// policy phases treat these differently; authentication treats them alike.
/// </summary>
public enum IdentityType
{
    User,
    Agent,
    Service,
}
