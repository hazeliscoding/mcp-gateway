using McpGateway.Application.Auditing;
using McpGateway.Domain.Auditing;

namespace McpGateway.WebApi.Endpoints;

/// <summary>
/// Read-only view of the audit trail. Every authorization decision and approval
/// event is recorded here, filterable by tool, actor, type, and time window. The
/// trail spans every identity's activity, so reads are operator-only
/// (<c>gateway.admin</c>) — an agent must not enumerate other callers' actions.
/// </summary>
public static class AuditEndpoints
{
    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder app)
    {
        var audit = app.MapGroup("/api/audit").RequireAuthorization(AuthorizationPolicies.AdminScope);

        audit.MapGet("/", async (
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
        });

        audit.MapGet("/stats", async (
            AuditService service,
            CancellationToken cancellationToken,
            DateTimeOffset? from = null,
            DateTimeOffset? to = null) =>
        {
            var result = await service.GetStatsAsync(new AuditStatsFilter(from, to), cancellationToken);
            return result.ToHttp(Results.Ok);
        });

        return app;
    }
}
