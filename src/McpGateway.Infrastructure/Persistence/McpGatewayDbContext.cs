using McpGateway.Domain.Approvals;
using McpGateway.Domain.Auditing;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;
using Microsoft.EntityFrameworkCore;

namespace McpGateway.Infrastructure.Persistence;

public sealed class McpGatewayDbContext(DbContextOptions<McpGatewayDbContext> options) : DbContext(options)
{
    public DbSet<ToolDefinition> Tools => Set<ToolDefinition>();

    public DbSet<GatewayIdentity> Identities => Set<GatewayIdentity>();

    public DbSet<ApprovalRequest> Approvals => Set<ApprovalRequest>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(McpGatewayDbContext).Assembly);
}
