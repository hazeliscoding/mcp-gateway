using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;
using Microsoft.EntityFrameworkCore;

namespace McpGateway.Infrastructure.Persistence;

public sealed class McpGatewayDbContext(DbContextOptions<McpGatewayDbContext> options) : DbContext(options)
{
    public DbSet<ToolDefinition> Tools => Set<ToolDefinition>();

    public DbSet<GatewayIdentity> Identities => Set<GatewayIdentity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(McpGatewayDbContext).Assembly);
}
