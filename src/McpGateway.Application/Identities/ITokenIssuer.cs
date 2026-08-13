using McpGateway.Domain.Identities;

namespace McpGateway.Application.Identities;

/// <summary>Claims the gateway asserts about an authenticated principal.</summary>
public sealed record TokenSubject(string ClientId, IdentityType Type, IReadOnlyList<string> Scopes);

/// <summary>A signed access token and its lifetime in seconds.</summary>
public sealed record IssuedToken(string AccessToken, int ExpiresInSeconds);

/// <summary>Signs access tokens; the signing key never leaves the implementation.</summary>
public interface ITokenIssuer
{
    IssuedToken Issue(TokenSubject subject);
}
