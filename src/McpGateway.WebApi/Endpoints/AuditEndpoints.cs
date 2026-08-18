using McpGateway.Application.Auditing;
using McpGateway.Domain.Auditing;

namespace McpGateway.WebApi.Endpoints;

/// <summary>
/// Read-only view of the audit trail. Every authorization decision and approval
/// event is recorded here, filterable by tool, actor, type, and time window.
/// </summary>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/audit", async (
            AuditService service,
            CancellationToken cancellationToken,
            string? toolName = null,
            string? actor = null,
            AuditEventType? eventType = null,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null,
            int limit = 100) =>
        {
            var result = await service.ListAsync(
                new AuditQueryFilter(toolName, actor, eventType, from, to, limit), cancellationToken);
            return result.ToHttp(Results.Ok);
        }).RequireAuthorization();

        app.MapGet("/api/audit/stats", async (
            AuditService service,
            CancellationToken cancellationToken,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null) =>
        {
            var result = await service.GetStatsAsync(new AuditStatsFilter(from, to), cancellationToken);
            return result.ToHttp(Results.Ok);
        }).RequireAuthorization();

        return app;
    }
}
