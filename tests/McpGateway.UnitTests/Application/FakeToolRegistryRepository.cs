using McpGateway.Application.Tools;
using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Application;

/// <summary>In-memory repository; mutations of returned aggregates are visible immediately, as with a tracking ORM.</summary>
internal sealed class FakeToolRegistryRepository : IToolRegistryRepository
{
    private readonly Dictionary<string, ToolDefinition> _tools = [];

    public int SaveCount { get; private set; }

    public Task<ToolDefinition?> GetByNameAsync(ToolName name, CancellationToken cancellationToken) =>
        Task.FromResult(_tools.GetValueOrDefault(name.Value));

    public Task<IReadOnlyList<ToolDefinition>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ToolDefinition>>(_tools.Values.ToList());

    public Task AddAsync(ToolDefinition tool, CancellationToken cancellationToken)
    {
        _tools.Add(tool.Name.Value, tool);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
