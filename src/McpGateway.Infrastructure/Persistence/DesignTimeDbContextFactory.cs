using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace McpGateway.Infrastructure.Persistence;

/// <summary>
/// Lets `dotnet ef migrations add` build the context without starting the API.
/// The connection string is design-time only; runtime configuration comes from
/// the host.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<McpGatewayDbContext>
{
    public McpGatewayDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<McpGatewayDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=mcp_gateway;Username=postgres;Password=postgres")
            .Options);
}
