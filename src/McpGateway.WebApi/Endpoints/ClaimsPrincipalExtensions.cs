using System.Security.Claims;
using McpGateway.Application.Authorization;
using McpGateway.Domain;
using McpGateway.Domain.Identities;
using Microsoft.IdentityModel.JsonWebTokens;

namespace McpGateway.WebApi.Endpoints;

/// <summary>
/// Projects the validated token into a <see cref="CallerPrincipal"/>. This is the
/// single point where caller scopes enter the authorization flow — they come only
/// from signed token claims, never from request bodies, so a caller cannot widen
/// its own grant.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    public static CallerPrincipal? ToCallerPrincipal(this ClaimsPrincipal principal)
    {
        // `sub` is remapped to NameIdentifier when inbound claim mapping is on, so accept either.
        var subject = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);
        var identityType = principal.FindFirstValue("identity_type");
        if (subject is null || identityType is null || !Enum.TryParse<IdentityType>(identityType, out var type))
        {
            return null;
        }

        ClientId clientId;
        try
        {
            clientId = ClientId.Create(subject);
        }
        catch (DomainException)
        {
            return null;
        }

        // Scopes are issued as a single space-delimited claim (see JwtTokenIssuer).
        var scopes = (principal.FindFirstValue("scope") ?? string.Empty)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        return new CallerPrincipal(clientId, type, scopes);
    }
}
