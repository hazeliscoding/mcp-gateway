using McpGateway.Application.Tools;
using McpGateway.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace McpGateway.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMcpGatewayInfrastructure(
        this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<McpGatewayDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IToolRegistryRepository, ToolRegistryRepository>();
        services.AddScoped<ToolRegistryService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
