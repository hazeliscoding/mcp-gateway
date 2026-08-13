namespace McpGateway.Infrastructure.Security;

/// <summary>Token signing and validation settings, bound from the "Auth" configuration section.</summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>HS256 signing key; must be at least 32 bytes. Supplied via configuration, never committed for real deployments.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = "mcp-gateway";

    public string Audience { get; set; } = "mcp-gateway";

    public int TokenLifetimeMinutes { get; set; } = 15;
}
