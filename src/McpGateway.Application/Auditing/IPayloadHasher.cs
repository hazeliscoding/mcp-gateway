namespace McpGateway.Application.Auditing;

/// <summary>
/// Hashes request context for the audit trail so sensitive values are recorded as
/// a fingerprint rather than stored raw (redaction).
/// </summary>
public interface IPayloadHasher
{
    string Hash(string value);
}
