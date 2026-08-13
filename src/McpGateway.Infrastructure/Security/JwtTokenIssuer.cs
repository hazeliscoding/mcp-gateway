using System.Text;
using McpGateway.Application.Identities;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace McpGateway.Infrastructure.Security;

/// <summary>
/// Issues HS256-signed JWTs. The signing key stays inside this class; callers
/// (and therefore any model output paths) only ever see the signed token.
/// </summary>
public sealed class JwtTokenIssuer : ITokenIssuer
{
    private readonly AuthOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly SigningCredentials _credentials;

    public JwtTokenIssuer(IOptions<AuthOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;

        if (Encoding.UTF8.GetByteCount(_options.SigningKey) < 32)
        {
            throw new InvalidOperationException(
                "Auth:SigningKey must be configured with at least 32 bytes for HS256.");
        }

        _credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey)),
            SecurityAlgorithms.HmacSha256);
    }

    public IssuedToken Issue(TokenSubject subject)
    {
        var now = _timeProvider.GetUtcNow();
        var lifetime = TimeSpan.FromMinutes(_options.TokenLifetimeMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = now.Add(lifetime).UtcDateTime,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = subject.ClientId,
                ["identity_type"] = subject.Type.ToString(),
                ["scope"] = string.Join(' ', subject.Scopes),
            },
        };

        return new IssuedToken(new JsonWebTokenHandler().CreateToken(descriptor), (int)lifetime.TotalSeconds);
    }
}
