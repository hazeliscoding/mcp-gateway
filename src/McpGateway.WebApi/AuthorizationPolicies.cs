using Microsoft.AspNetCore.Authorization;

namespace McpGateway.WebApi;

/// <summary>
/// Endpoint authorization policies. Scopes are issued as a single space-delimited
/// claim (see JwtTokenIssuer), so policies assert on the split claim value rather
/// than using <c>RequireClaim</c>, which only matches whole claim values.
/// </summary>
public static class AuthorizationPolicies
{
    /// <summary>Policy name for endpoints that manage the gateway itself.</summary>
    public const string AdminScope = "AdminScope";

    /// <summary>Scope granted to operators; seeded onto the bootstrap identity.</summary>
    public const string GatewayAdminScope = "gateway.admin";

    public static AuthorizationOptions AddGatewayPolicies(this AuthorizationOptions options)
    {
        options.AddPolicy(AdminScope, policy => policy
            .RequireAuthenticatedUser()
            .RequireAssertion(context =>
                (context.User.FindFirst("scope")?.Value ?? string.Empty)
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Contains(GatewayAdminScope)));
        return options;
    }
}
