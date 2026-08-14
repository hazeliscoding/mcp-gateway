using System.Security.Claims;
using McpGateway.Application.Authorization;

namespace McpGateway.WebApi.Endpoints;

/// <summary>
/// The authorization decision point. A caller asks whether it may act on a tool;
/// the gateway answers permit/deny with reasons. Later phases (approval, audit)
/// and real tool execution call through this same decision. The caller's scopes
/// are read from its token, never from the request body.
/// </summary>
public static class AuthorizationEndpoints
{
    public static IEndpointRouteBuilder MapAuthorizationEndpoints(this IEndpointRouteBuilder app)
    {
        var tools = app.MapGroup("/api/tools").RequireAuthorization();

        tools.MapPost("/{name}/authorize", async (
            string name,
            AuthorizeToolRequest request,
            ClaimsPrincipal user,
            AuthorizationService service,
            CancellationToken cancellationToken) =>
        {
            var caller = user.ToCallerPrincipal();
            if (caller is null)
            {
                return Results.Unauthorized();
            }

            // A deny is a successful evaluation: return 200 with the decision body.
            var result = await service.AuthorizeToolAsync(name, request, caller, cancellationToken);
            return result.ToHttp(Results.Ok);
        });

        return app;
    }
}
