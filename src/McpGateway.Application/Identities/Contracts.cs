using McpGateway.Domain.Identities;

namespace McpGateway.Application.Identities;

/// <summary>Registers a new identity; the gateway generates the client secret.</summary>
public sealed record RegisterIdentityRequest(
    string ClientId,
    IdentityType Type,
    string DisplayName,
    IReadOnlyList<string> GrantedScopes);

public sealed record IdentityResponse(
    string ClientId,
    IdentityType Type,
    string DisplayName,
    IReadOnlyList<string> GrantedScopes,
    bool Enabled,
    DateTimeOffset CreatedAt);

/// <summary>
/// Returned only from registration and secret rotation — the single time the
/// raw secret is ever visible. It is stored hashed and cannot be retrieved.
/// </summary>
public sealed record IssuedSecretResponse(IdentityResponse Identity, string ClientSecret);

/// <summary>OAuth2 token response (RFC 6749 §5.1).</summary>
public sealed record TokenResponse(string AccessToken, string TokenType, int ExpiresIn);
