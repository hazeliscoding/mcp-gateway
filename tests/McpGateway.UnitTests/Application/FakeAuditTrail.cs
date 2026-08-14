using McpGateway.Application.Auditing;
using McpGateway.Application.Authorization;
using McpGateway.Domain.Approvals;
using McpGateway.Domain.Auditing;
using McpGateway.Domain.Authorization;
using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Application;

/// <summary>Captures what the services recorded so tests can assert on the audit calls.</summary>
internal sealed class FakeAuditTrail : IAuditTrail
{
    public List<(AuthorizationOutcome Outcome, string ToolName, string CanonicalInput)> Authorizations { get; } = [];
    public List<(AuditEventType EventType, Guid ApprovalId)> Approvals { get; } = [];

    public Task RecordAuthorizationAsync(
        CallerPrincipal caller, ToolName toolName, ToolVersionNumber? version, ToolAction action,
        string canonicalInput, AuthorizationOutcome outcome, IReadOnlyList<AuthorizationReasonCode> reasonCodes,
        CancellationToken cancellationToken)
    {
        Authorizations.Add((outcome, toolName.Value, canonicalInput));
        return Task.CompletedTask;
    }

    public Task RecordApprovalAsync(
        AuditEventType eventType, CallerPrincipal actor, ApprovalRequest approval, CancellationToken cancellationToken)
    {
        Approvals.Add((eventType, approval.Id));
        return Task.CompletedTask;
    }
}
