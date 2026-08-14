using McpGateway.Domain.Auditing;

namespace McpGateway.Application.Auditing;

/// <summary>Append-only persistence for the audit trail. No update or delete.</summary>
public interface IAuditRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);

    /// <summary>Returns entries matching the filter, newest first, bounded by the filter's limit.</summary>
    Task<IReadOnlyList<AuditEntry>> QueryAsync(AuditQueryFilter filter, CancellationToken cancellationToken);
}
