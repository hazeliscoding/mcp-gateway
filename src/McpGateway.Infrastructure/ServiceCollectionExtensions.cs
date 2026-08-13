using McpGateway.Application.Identities;
using McpGateway.Application.Tools;
using McpGateway.Infrastructure.Persistence;
using McpGateway.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace McpGateway.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMcpGatewayInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("McpGateway")
            ?? throw new InvalidOperationException("Connection string 'McpGateway' is not configured.");

        services.AddDbContext<McpGatewayDbContext>(options => options.UseNpgsql(connectionString));
        services.Configure<AuthOptions>(configuration.GetSection(AuthOptions.SectionName));

        services.AddScoped<IToolRegistryRepository, ToolRegistryRepository>();
        services.AddScoped<ToolRegistryService>();

        services.AddScoped<IIdentityRepository, IdentityRepository>();
        services.AddSingleton<ISecretHasher, Pbkdf2SecretHasher>();
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<IdentityService>();
        services.AddScoped<TokenService>();

        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
