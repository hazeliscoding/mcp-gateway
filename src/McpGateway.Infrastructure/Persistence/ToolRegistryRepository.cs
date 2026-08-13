using McpGateway.Application.Tools;
using McpGateway.Domain.Tools;
using Microsoft.EntityFrameworkCore;

namespace McpGateway.Infrastructure.Persistence;

public sealed class ToolRegistryRepository(McpGatewayDbContext dbContext) : IToolRegistryRepository
{
    public async Task<ToolDefinition?> GetByNameAsync(ToolName name, CancellationToken cancellationToken) =>
        await dbContext.Tools.FirstOrDefaultAsync(t => t.Name == name, cancellationToken);

    public async Task<IReadOnlyList<ToolDefinition>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Tools.ToListAsync(cancellationToken);

    public Task AddAsync(ToolDefinition tool, CancellationToken cancellationToken)
    {
        dbContext.Tools.Add(tool);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
