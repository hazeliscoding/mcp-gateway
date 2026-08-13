using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;

namespace McpGateway.IntegrationTests;

/// <summary>Boots the real API pipeline against the shared Testcontainers Postgres instance.</summary>
public sealed class GatewayApiFactory(string connectionString) : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseSetting("ConnectionStrings:McpGateway", connectionString);
}
