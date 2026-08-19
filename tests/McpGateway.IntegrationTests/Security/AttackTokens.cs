using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace McpGateway.IntegrationTests.Security;

/// <summary>
/// Mints hand-crafted tokens for authentication attacks — a forger who controls the
/// claims but not the gateway's signing key, or who replays an expired token.
/// </summary>
internal static class AttackTokens
{
    /// <summary>A 32-byte key that is NOT the gateway's — signatures made with it must fail validation.</summary>
    public const string WrongSigningKey = "attacker-controlled-key-not-the-servers!!";

    public static string Forge(
        string signingKey,
        DateTimeOffset expires,
        string subject = "forged_admin",
        string scope = "gateway.admin",
        string issuer = "mcp-gateway",
        string audience = "mcp-gateway")
    {
        var credentials = new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256);
        var issuedAt = expires.AddMinutes(-15).UtcDateTime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expires.UtcDateTime,
            SigningCredentials = credentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = subject,
                ["identity_type"] = "Service",
                ["scope"] = scope,
            },
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Flips a character in the payload segment so the signature no longer matches.</summary>
    public static string Tamper(string token)
    {
        var parts = token.Split('.');
        var payload = parts[1];
        // Swap the last payload character for a different base64url character.
        var last = payload[^1];
        var replacement = last == 'A' ? 'B' : 'A';
        parts[1] = payload[..^1] + replacement;
        return string.Join('.', parts);
    }
}
