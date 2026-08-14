using McpGateway.Application.Auditing;
using McpGateway.Domain.Auditing;

namespace McpGateway.UnitTests.Application;

/// <summary>In-memory audit store with the same filtering semantics as the real repository.</summary>
internal sealed class FakeAuditRepository : IAuditRepository
{
    private readonly List<AuditEntry> _entries = [];

    public IReadOnlyList<AuditEntry> Entries => _entries;

    public Task AddAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        _entries.Add(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task<IReadOnlyList<AuditEntry>> QueryAsync(AuditQueryFilter filter, CancellationToken cancellationToken)
    {
        IReadOnlyList<AuditEntry> results = _entries
            .Where(a => filter.ToolName == null || a.ToolName?.Value == filter.ToolName)
            .Where(a => filter.Actor == null || a.ActorClientId.Value == filter.Actor)
            .Where(a => filter.EventType == null || a.EventType == filter.EventType)
            .Where(a => filter.From == null || a.OccurredAt >= filter.From)
            .Where(a => filter.To == null || a.OccurredAt <= filter.To)
            .OrderByDescending(a => a.OccurredAt)
            .Take(filter.Limit)
            .ToList();
        return Task.FromResult(results);
    }
}
