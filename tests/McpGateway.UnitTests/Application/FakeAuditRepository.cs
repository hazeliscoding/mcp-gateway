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

    public Task<AuditStatsResponse> GetStatsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        var rows = _entries.Where(a => a.OccurredAt >= from && a.OccurredAt <= to).ToList();

        var byType = rows.GroupBy(r => r.EventType.ToString())
            .Select(g => new NamedCount(g.Key, g.Count())).OrderByDescending(c => c.Count).ThenBy(c => c.Name).ToList();
        var byTool = rows.Where(r => r.ToolName is not null).GroupBy(r => r.ToolName!.Value)
            .Select(g => new NamedCount(g.Key, g.Count())).OrderByDescending(c => c.Count).ThenBy(c => c.Name).Take(10).ToList();
        var outcomes = rows.Where(r => r.EventType == AuditEventType.AuthorizationDecision).GroupBy(r => r.Result)
            .Select(g => new NamedCount(g.Key, g.Count())).OrderByDescending(c => c.Count).ThenBy(c => c.Name).ToList();
        var byActor = rows.GroupBy(r => r.ActorClientId.Value)
            .Select(g => new NamedCount(g.Key, g.Count())).OrderByDescending(c => c.Count).ThenBy(c => c.Name).Take(10).ToList();
        var perDay = rows.GroupBy(r => DateOnly.FromDateTime(r.OccurredAt.UtcDateTime))
            .Select(g => new DailyCount(g.Key, g.Count())).OrderBy(c => c.Date).ToList();

        return Task.FromResult(new AuditStatsResponse(from, to, rows.Count, byType, byTool, outcomes, byActor, perDay));
    }
}
