using System.Security.Claims;
using McpGateway.Application.Approvals;
using McpGateway.Domain.Approvals;

namespace McpGateway.WebApi.Endpoints;

/// <summary>
/// The approval workflow that makes a <c>RequiresApproval</c> decision actionable.
/// A requester opens a request for a risk-gated tool; a different principal approves
/// or rejects it (four-eyes is enforced in the domain). Requester and approver are
/// always taken from the token, never the request body.
/// </summary>
public static class ApprovalEndpoints
{
    public static IEndpointRouteBuilder MapApprovalEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/api/tools/{name}/approvals", async (
            string name,
            RequestApprovalCommand command,
            ClaimsPrincipal user,
            ApprovalService service,
            CancellationToken cancellationToken) =>
        {
            var caller = user.ToCallerPrincipal();
            if (caller is null)
            {
                return Results.Unauthorized();
            }

            var result = await service.RequestApprovalAsync(name, command, caller, cancellationToken);
            return result.ToHttp(approval => Results.Created($"/api/approvals/{approval.Id}", approval));
        }).RequireAuthorization();

        var approvals = app.MapGroup("/api/approvals").RequireAuthorization();

        approvals.MapGet("/", async (ApprovalService service, CancellationToken cancellationToken, ApprovalStatus? status = null) =>
            (await service.ListAsync(status, cancellationToken)).ToHttp(Results.Ok));

        approvals.MapGet("/{id:guid}", async (Guid id, ApprovalService service, CancellationToken cancellationToken) =>
            (await service.GetAsync(id, cancellationToken)).ToHttp(Results.Ok));

        // Deciding is operator-only; requesting and reading stay open because agents
        // open approval requests and poll their status themselves.
        approvals.MapPost("/{id:guid}/approve", (Guid id, DecisionRequest? body, ClaimsPrincipal user, ApprovalService service, CancellationToken cancellationToken) =>
            DecideAsync(id, body, user, service, approve: true, cancellationToken))
            .RequireAuthorization(AuthorizationPolicies.AdminScope);

        approvals.MapPost("/{id:guid}/reject", (Guid id, DecisionRequest? body, ClaimsPrincipal user, ApprovalService service, CancellationToken cancellationToken) =>
            DecideAsync(id, body, user, service, approve: false, cancellationToken))
            .RequireAuthorization(AuthorizationPolicies.AdminScope);

        return app;
    }

    private static async Task<IResult> DecideAsync(
        Guid id, DecisionRequest? body, ClaimsPrincipal user, ApprovalService service, bool approve,
        CancellationToken cancellationToken)
    {
        var caller = user.ToCallerPrincipal();
        if (caller is null)
        {
            return Results.Unauthorized();
        }

        var note = body?.Note;
        var result = approve
            ? await service.ApproveAsync(id, caller, note, cancellationToken)
            : await service.RejectAsync(id, caller, note, cancellationToken);
        return result.ToHttp(Results.Ok);
    }
}
