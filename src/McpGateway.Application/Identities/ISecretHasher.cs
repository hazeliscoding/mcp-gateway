namespace McpGateway.Application.Identities;

/// <summary>
/// One-way hashing for client secrets. Verification must be constant-time so
/// token requests cannot be used as a timing oracle.
/// </summary>
public interface ISecretHasher
{
    string Hash(string secret);

    bool Verify(string secret, string storedHash);
}
