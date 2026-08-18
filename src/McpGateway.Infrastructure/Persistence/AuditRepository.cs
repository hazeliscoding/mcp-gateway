using McpGateway.Application.Auditing;
using McpGateway.Domain;
using McpGateway.Domain.Auditing;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;
using Microsoft.EntityFrameworkCore;

namespace McpGateway.Infrastructure.Persistence;

public sealed class AuditRepository(McpGatewayDbContext dbContext) : IAuditRepository
{
    public Task AddAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        dbContext.AuditEntries.Add(entry);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);

    public async Task<IReadOnlyList<AuditEntry>> QueryAsync(
        AuditQueryFilter filter, CancellationToken cancellationToken)
    {
        var query = dbContext.AuditEntries.AsQueryable();

        if (filter.ToolName is not null)
        {
            // A malformed filter matches nothing rather than throwing.
            if (!TryCreate(filter.ToolName, ToolName.Create, out var toolName))
            {
                return [];
            }

            query = query.Where(a => a.ToolName == toolName);
        }

        if (filter.Actor is not null)
        {
            if (!TryCreate(filter.Actor, ClientId.Create, out var actor))
            {
                return [];
            }

            query = query.Where(a => a.ActorClientId == actor);
        }

        if (filter.EventType is { } eventType)
        {
            query = query.Where(a => a.EventType == eventType);
        }

        if (filter.From is { } from)
        {
            query = query.Where(a => a.OccurredAt >= from);
        }

        if (filter.To is { } to)
        {
            query = query.Where(a => a.OccurredAt <= to);
        }

        return await query
            .OrderByDescending(a => a.OccurredAt)
            .Take(filter.Limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<AuditStatsResponse> GetStatsAsync(
        DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken)
    {
        // The window is filtered server-side (translates cleanly on the OccurredAt index);
        // the grouping is done in memory to avoid translating GroupBy over value-object
        // converters and per-day date truncation. The window bounds the row count.
        var rows = await dbContext.AuditEntries
            .Where(a => a.OccurredAt >= from && a.OccurredAt <= to)
            .Select(a => new { a.OccurredAt, a.EventType, a.ToolName, a.Result, a.ActorClientId })
            .ToListAsync(cancellationToken);

        var eventsByType = rows
            .GroupBy(r => r.EventType.ToString())
            .Select(g => new NamedCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name)
            .ToList();

        var eventsByTool = rows
            .Where(r => r.ToolName is not null)
            .GroupBy(r => r.ToolName!.Value)
            .Select(g => new NamedCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name)
            .Take(10)
            .ToList();

        var authorizationOutcomes = rows
            .Where(r => r.EventType == AuditEventType.AuthorizationDecision)
            .GroupBy(r => r.Result)
            .Select(g => new NamedCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name)
            .ToList();

        var eventsByActor = rows
            .GroupBy(r => r.ActorClientId.Value)
            .Select(g => new NamedCount(g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Name)
            .Take(10)
            .ToList();

        var eventsPerDay = rows
            .GroupBy(r => DateOnly.FromDateTime(r.OccurredAt.UtcDateTime))
            .Select(g => new DailyCount(g.Key, g.Count()))
            .OrderBy(c => c.Date)
            .ToList();

        return new AuditStatsResponse(
            from, to, rows.Count, eventsByType, eventsByTool, authorizationOutcomes, eventsByActor, eventsPerDay);
    }

    private static bool TryCreate<T>(string value, Func<string, T> factory, out T created)
    {
        try
        {
            created = factory(value);
            return true;
        }
        catch (DomainException)
        {
            created = default!;
            return false;
        }
    }
}
