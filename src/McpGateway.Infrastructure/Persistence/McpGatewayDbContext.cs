using McpGateway.Domain.Tools;
using Microsoft.EntityFrameworkCore;

namespace McpGateway.Infrastructure.Persistence;

public sealed class McpGatewayDbContext(DbContextOptions<McpGatewayDbContext> options) : DbContext(options)
{
    public DbSet<ToolDefinition> Tools => Set<ToolDefinition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(McpGatewayDbContext).Assembly);
}
