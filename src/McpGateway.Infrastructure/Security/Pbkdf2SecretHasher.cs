using System.Security.Cryptography;
using McpGateway.Application.Identities;

namespace McpGateway.Infrastructure.Security;

/// <summary>
/// PBKDF2-SHA256 secret hashing with a per-secret random salt. Stored format:
/// <c>pbkdf2-sha256.{iterations}.{salt}.{key}</c> — iterations are embedded so
/// they can be raised later without invalidating existing hashes.
/// </summary>
public sealed class Pbkdf2SecretHasher : ISecretHasher
{
    private const string Prefix = "pbkdf2-sha256";
    private const int SaltSizeBytes = 16;
    private const int KeySizeBytes = 32;
    private const int Iterations = 100_000;
    private static readonly HashAlgorithmName Algorithm = HashAlgorithmName.SHA256;

    public string Hash(string secret)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeBytes);
        var key = Rfc2898DeriveBytes.Pbkdf2(secret, salt, Iterations, Algorithm, KeySizeBytes);
        return $"{Prefix}.{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(key)}";
    }

    public bool Verify(string secret, string storedHash)
    {
        var parts = storedHash.Split('.');
        if (parts.Length != 4 || parts[0] != Prefix || !int.TryParse(parts[1], out var iterations) || iterations < 1)
        {
            return false;
        }

        try
        {
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);
            var actual = Rfc2898DeriveBytes.Pbkdf2(secret, salt, iterations, Algorithm, expected.Length);
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
