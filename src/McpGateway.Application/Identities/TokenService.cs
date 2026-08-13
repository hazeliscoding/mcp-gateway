using McpGateway.Domain;
using McpGateway.Domain.Identities;
using Microsoft.Extensions.Logging;

namespace McpGateway.Application.Identities;

/// <summary>
/// OAuth2 client-credentials token issuance. Unknown client, wrong secret,
/// and disabled identity all produce the identical failure so the endpoint
/// cannot be used to enumerate registered identities.
/// </summary>
public sealed class TokenService(
    IIdentityRepository repository,
    ISecretHasher secretHasher,
    ITokenIssuer tokenIssuer,
    ILogger<TokenService> logger)
{
    private const string InvalidClientMessage = "Invalid client credentials.";

    /// <summary>Hash verified against when the client id is unknown, keeping timing uniform.</summary>
    private readonly Lazy<string> _decoyHash = new(() => secretHasher.Hash(Guid.NewGuid().ToString("N")));

    public async Task<OperationResult<TokenResponse>> IssueTokenAsync(
        string clientId, string clientSecret, CancellationToken cancellationToken)
    {
        ClientId id;
        try
        {
            id = ClientId.Create(clientId ?? string.Empty);
        }
        catch (DomainException)
        {
            return OperationResult<TokenResponse>.Invalid(InvalidClientMessage);
        }

        var identity = await repository.GetByClientIdAsync(id, cancellationToken);

        var secretMatches = secretHasher.Verify(clientSecret ?? string.Empty, identity?.SecretHash ?? _decoyHash.Value);
        if (identity is null || !secretMatches || !identity.Enabled)
        {
            logger.LogWarning("Token request rejected for client {ClientId}", clientId);
            return OperationResult<TokenResponse>.Invalid(InvalidClientMessage);
        }

        var token = tokenIssuer.Issue(new TokenSubject(
            identity.ClientId.Value, identity.Type, identity.GrantedScopes));

        logger.LogInformation("Issued token for {IdentityType} identity {ClientId}", identity.Type, identity.ClientId.Value);
        return OperationResult<TokenResponse>.Success(
            new TokenResponse(token.AccessToken, "Bearer", token.ExpiresInSeconds));
    }
}
