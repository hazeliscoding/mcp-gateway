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
