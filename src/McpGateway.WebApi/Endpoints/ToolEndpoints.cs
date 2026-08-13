using McpGateway.Application;
using McpGateway.Application.Tools;
using McpGateway.Domain.Tools;

namespace McpGateway.WebApi.Endpoints;

/// <summary>
/// HTTP surface of the tool registry. Endpoints only bind input, invoke the
/// application service, and translate <see cref="OperationResult{T}"/> to
/// status codes — business rules live in the domain and application layers.
/// </summary>
public static class ToolEndpoints
{
    public static IEndpointRouteBuilder MapToolEndpoints(this IEndpointRouteBuilder app)
    {
        var tools = app.MapGroup("/api/tools").RequireAuthorization();

        tools.MapPost("/", async (RegisterToolRequest request, ToolRegistryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RegisterToolAsync(request, cancellationToken);
            return result.ToHttp(detail => Results.Created($"/api/tools/{detail.Name}", detail));
        });

        tools.MapPost("/{name}/versions", async (string name, RegisterVersionRequest request, ToolRegistryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RegisterVersionAsync(name, request, cancellationToken);
            return result.ToHttp(detail => Results.Created($"/api/tools/{detail.Name}", detail));
        });

        tools.MapGet("/", async (ToolRegistryService service, CancellationToken cancellationToken, RiskLevel? riskLevel = null, bool includeDisabled = false, string? nameContains = null) =>
        {
            var result = await service.ListToolsAsync(
                new ToolDiscoveryFilter(riskLevel, includeDisabled, nameContains), cancellationToken);
            return result.ToHttp(Results.Ok);
        });

        tools.MapGet("/{name}", async (string name, ToolRegistryService service, CancellationToken cancellationToken) =>
        {
            var result = await service.GetToolAsync(name, cancellationToken);
            return result.ToHttp(Results.Ok);
        });

        tools.MapPost("/{name}/enable", async (string name, ToolRegistryService service, CancellationToken cancellationToken) =>
            (await service.SetEnabledAsync(name, enabled: true, cancellationToken)).ToHttp(_ => Results.NoContent()));

        tools.MapPost("/{name}/disable", async (string name, ToolRegistryService service, CancellationToken cancellationToken) =>
            (await service.SetEnabledAsync(name, enabled: false, cancellationToken)).ToHttp(_ => Results.NoContent()));

        tools.MapPost("/{name}/versions/{version}/deprecate", async (string name, string version, ToolRegistryService service, CancellationToken cancellationToken) =>
            (await service.DeprecateVersionAsync(name, version, cancellationToken)).ToHttp(_ => Results.NoContent()));

        return app;
    }
}
