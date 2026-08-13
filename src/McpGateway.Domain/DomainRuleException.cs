namespace McpGateway.Domain;

/// <summary>
/// Base for domain invariant violations. The application layer translates these
/// into typed operation results rather than letting them escape as 500s.
/// </summary>
public abstract class DomainException(string message) : Exception(message);

/// <summary>The supplied data is invalid regardless of current state (maps to a validation error).</summary>
public sealed class DomainRuleException(string message) : DomainException(message);

/// <summary>The operation clashes with existing state, e.g. a duplicate or lower version (maps to a conflict).</summary>
public sealed class DomainConflictException(string message) : DomainException(message);
