using McpGateway.Domain.Identities;

namespace McpGateway.Application.Authorization;

/// <summary>
/// The authenticated caller as the application layer sees it, projected from the
/// validated token by the API. Scopes originate from the token and nowhere else,
/// so a caller can never widen its own grant through the request body.
/// </summary>
/// <param name="ClientId">The token's subject.</param>
/// <param name="Type">Kind of principal.</param>
/// <param name="GrantedScopes">Scopes carried by the token.</param>
public sealed record CallerPrincipal(
    ClientId ClientId,
    IdentityType Type,
    IReadOnlyList<string> GrantedScopes);
