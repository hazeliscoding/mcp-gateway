using System.Buffers.Text;
using System.Security.Cryptography;
using McpGateway.Domain;
using McpGateway.Domain.Identities;
using Microsoft.Extensions.Logging;

namespace McpGateway.Application.Identities;

/// <summary>
/// Identity management commands and queries. Client secrets are generated
/// here, returned to the caller exactly once, and persisted only as hashes.
/// </summary>
public sealed class IdentityService(
    IIdentityRepository repository,
    ISecretHasher secretHasher,
    TimeProvider timeProvider,
    ILogger<IdentityService> logger)
{
    private const int SecretSizeBytes = 32;

    public async Task<OperationResult<IssuedSecretResponse>> RegisterIdentityAsync(
        RegisterIdentityRequest request, CancellationToken cancellationToken)
    {
        GatewayIdentity identity;
        string secret;
        try
        {
            var clientId = ClientId.Create(request.ClientId);
            if (await repository.GetByClientIdAsync(clientId, cancellationToken) is not null)
            {
                return OperationResult<IssuedSecretResponse>.Conflict($"Identity '{clientId}' is already registered.");
            }

            secret = GenerateSecret();
            identity = GatewayIdentity.Register(
                clientId, request.Type, request.DisplayName, secretHasher.Hash(secret),
                request.GrantedScopes, timeProvider.GetUtcNow());
        }
        catch (DomainException ex)
        {
            return OperationResult<IssuedSecretResponse>.Invalid(ex.Message);
        }

        await repository.AddAsync(identity, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Registered {IdentityType} identity {ClientId}", identity.Type, identity.ClientId.Value);
        return OperationResult<IssuedSecretResponse>.Success(new IssuedSecretResponse(ToResponse(identity), secret));
    }

    public async Task<OperationResult<IssuedSecretResponse>> RotateSecretAsync(
        string clientId, CancellationToken cancellationToken)
    {
        var (identity, failure) = await FindAsync<IssuedSecretResponse>(clientId, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        var secret = GenerateSecret();
        identity!.RotateSecret(secretHasher.Hash(secret));
        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Rotated secret for identity {ClientId}", identity.ClientId.Value);
        return OperationResult<IssuedSecretResponse>.Success(new IssuedSecretResponse(ToResponse(identity), secret));
    }

    public async Task<OperationResult<bool>> SetEnabledAsync(
        string clientId, bool enabled, CancellationToken cancellationToken)
    {
        var (identity, failure) = await FindAsync<bool>(clientId, cancellationToken);
        if (failure is not null)
        {
            return failure;
        }

        if (enabled)
        {
            identity!.Enable();
        }
        else
        {
            identity!.Disable();
        }

        await repository.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Identity {ClientId} {IdentityState}", identity.ClientId.Value, enabled ? "enabled" : "disabled");
        return OperationResult<bool>.Success(enabled);
    }

    public async Task<OperationResult<IdentityResponse>> GetAsync(string clientId, CancellationToken cancellationToken)
    {
        var (identity, failure) = await FindAsync<IdentityResponse>(clientId, cancellationToken);
        return failure ?? OperationResult<IdentityResponse>.Success(ToResponse(identity!));
    }

    public async Task<OperationResult<IReadOnlyList<IdentityResponse>>> ListAsync(CancellationToken cancellationToken)
    {
        var identities = await repository.ListAsync(cancellationToken);
        IReadOnlyList<IdentityResponse> responses = identities
            .OrderBy(i => i.ClientId.Value)
            .Select(ToResponse)
            .ToList();
        return OperationResult<IReadOnlyList<IdentityResponse>>.Success(responses);
    }

    private async Task<(GatewayIdentity? Identity, OperationResult<T>? Failure)> FindAsync<T>(
        string clientId, CancellationToken cancellationToken)
    {
        ClientId id;
        try
        {
            id = ClientId.Create(clientId);
        }
        catch (DomainException ex)
        {
            return (null, OperationResult<T>.Invalid(ex.Message));
        }

        var identity = await repository.GetByClientIdAsync(id, cancellationToken);
        return identity is null
            ? (null, OperationResult<T>.NotFound($"Identity '{id}' is not registered."))
            : (identity, null);
    }

    private static string GenerateSecret() =>
        Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(SecretSizeBytes));

    private static IdentityResponse ToResponse(GatewayIdentity identity) =>
        new(
            identity.ClientId.Value,
            identity.Type,
            identity.DisplayName,
            identity.GrantedScopes,
            identity.Enabled,
            identity.CreatedAt);
}
