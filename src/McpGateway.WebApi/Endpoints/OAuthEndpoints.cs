using McpGateway.Application.Identities;

namespace McpGateway.WebApi.Endpoints;

/// <summary>
/// OAuth2 token endpoint (RFC 6749). Only the client-credentials grant is
/// supported; failures use the spec's error codes and never reveal whether a
/// client id exists.
/// </summary>
public static class OAuthEndpoints
{
    public static IEndpointRouteBuilder MapOAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/oauth/token", async (HttpRequest request, TokenService tokenService, CancellationToken cancellationToken) =>
        {
            if (!request.HasFormContentType)
            {
                return OAuthError("invalid_request", StatusCodes.Status400BadRequest);
            }

            var form = await request.ReadFormAsync(cancellationToken);
            if (form["grant_type"].ToString() != "client_credentials")
            {
                return OAuthError("unsupported_grant_type", StatusCodes.Status400BadRequest);
            }

            var clientId = form["client_id"].ToString();
            var clientSecret = form["client_secret"].ToString();
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
            {
                return OAuthError("invalid_request", StatusCodes.Status400BadRequest);
            }

            var result = await tokenService.IssueTokenAsync(clientId, clientSecret, cancellationToken);
            return result.IsSuccess
                ? Results.Ok(new
                {
                    access_token = result.Value!.AccessToken,
                    token_type = result.Value.TokenType,
                    expires_in = result.Value.ExpiresIn,
                })
                : OAuthError("invalid_client", StatusCodes.Status401Unauthorized);
        }).AllowAnonymous();

        return app;
    }

    private static IResult OAuthError(string error, int statusCode) =>
        Results.Json(new { error }, statusCode: statusCode);
}
