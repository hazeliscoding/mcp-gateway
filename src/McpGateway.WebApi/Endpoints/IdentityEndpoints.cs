using McpGateway.Application.Identities;

namespace McpGateway.WebApi.Endpoints;

/// <summary>
/// Identity administration. Registration and rotation responses are the only
/// places a client secret ever appears; reads return metadata only.
/// </summary>
public static class IdentityEndpoints
{
    public static IEndpointRouteBuilder MapIdentityEndpoints(this IEndpointRouteBuilder app)
    {
        var identities = app.MapGroup("/api/identities").RequireAuthorization();

        identities.MapPost("/", async (RegisterIdentityRequest request, IdentityService service, CancellationToken cancellationToken) =>
        {
            var result = await service.RegisterIdentityAsync(request, cancellationToken);
            return result.ToHttp(issued => Results.Created($"/api/identities/{issued.Identity.ClientId}", issued));
        });

        identities.MapGet("/", async (IdentityService service, CancellationToken cancellationToken) =>
            (await service.ListAsync(cancellationToken)).ToHttp(Results.Ok));

        identities.MapGet("/{clientId}", async (string clientId, IdentityService service, CancellationToken cancellationToken) =>
            (await service.GetAsync(clientId, cancellationToken)).ToHttp(Results.Ok));

        identities.MapPost("/{clientId}/enable", async (string clientId, IdentityService service, CancellationToken cancellationToken) =>
            (await service.SetEnabledAsync(clientId, enabled: true, cancellationToken)).ToHttp(_ => Results.NoContent()));

        identities.MapPost("/{clientId}/disable", async (string clientId, IdentityService service, CancellationToken cancellationToken) =>
            (await service.SetEnabledAsync(clientId, enabled: false, cancellationToken)).ToHttp(_ => Results.NoContent()));

        identities.MapPost("/{clientId}/rotate-secret", async (string clientId, IdentityService service, CancellationToken cancellationToken) =>
            (await service.RotateSecretAsync(clientId, cancellationToken)).ToHttp(Results.Ok));

        return app;
    }
}
