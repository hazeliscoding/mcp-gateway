namespace McpGateway.Domain;

/// <summary>
/// Thrown when an operation would violate a domain invariant.
/// Callers (the application layer) translate this into a validation/conflict
/// outcome rather than letting it escape as a 500.
/// </summary>
public sealed class DomainRuleException(string message) : Exception(message);
