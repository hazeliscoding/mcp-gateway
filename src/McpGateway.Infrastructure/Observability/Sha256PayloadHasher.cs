using System.Security.Cryptography;
using System.Text;
using McpGateway.Application.Auditing;

namespace McpGateway.Infrastructure.Observability;

/// <summary>
/// Hashes request context with SHA-256 (lowercase hex) so the audit trail records a
/// stable fingerprint of sensitive values instead of the values themselves.
/// </summary>
public sealed class Sha256PayloadHasher : IPayloadHasher
{
    public string Hash(string value) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value ?? string.Empty)));
}
