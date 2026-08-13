using McpGateway.Domain.Tools;

namespace McpGateway.Application.Tools;

/// <summary>
/// Persistence boundary for the tool registry. Discovery filtering happens in
/// <see cref="ToolRegistryService"/> — a registry holds at most hundreds of
/// tools, so filtering in memory keeps this interface (and its fakes) trivial.
/// </summary>
public interface IToolRegistryRepository
{
    Task<ToolDefinition?> GetByNameAsync(ToolName name, CancellationToken cancellationToken);

    Task<IReadOnlyList<ToolDefinition>> ListAsync(CancellationToken cancellationToken);

    Task AddAsync(ToolDefinition tool, CancellationToken cancellationToken);

    /// <summary>Flushes all pending changes (both adds and mutations of tracked aggregates).</summary>
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
