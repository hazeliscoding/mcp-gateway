using McpGateway.Application;
using McpGateway.Application.Approvals;
using McpGateway.Application.Authorization;
using McpGateway.Domain.Approvals;
using McpGateway.Domain.Auditing;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;
using Microsoft.Extensions.Logging.Abstractions;

namespace McpGateway.UnitTests.Application;

public class ApprovalServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeApprovalRepository _approvals = new();
    private readonly FakeToolRegistryRepository _tools = new();
    private readonly FakeAuditTrail _audit = new();
    private readonly ApprovalService _service;

    public ApprovalServiceTests()
    {
        _service = new ApprovalService(
            _approvals, _tools, _audit, new FixedTimeProvider(Now), NullLogger<ApprovalService>.Instance);
    }

    [Fact]
    public async Task Request_opens_a_pending_approval_for_a_privileged_tool()
    {
        await SeedTool("redrive_dead_letter_queue", RiskLevel.Privileged, "queue.redrive");

        var result = await _service.RequestApprovalAsync(
            "redrive_dead_letter_queue", new RequestApprovalCommand(), Requester(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Pending, result.Value!.Status);
        Assert.Equal("incident_agent", result.Value.RequesterClientId);
        Assert.Equal("1.0.0", result.Value.Version);
        Assert.Equal(1, _approvals.SaveCount);
    }

    [Fact]
    public async Task Request_for_an_automatic_tool_is_rejected()
    {
        await SeedTool("get_queue_metrics", RiskLevel.ReadOnly, "queue.read");

        var result = await _service.RequestApprovalAsync(
            "get_queue_metrics", new RequestApprovalCommand(), Requester(), CancellationToken.None);

        Assert.Equal(OperationError.Validation, result.Error);
        Assert.Equal(0, _approvals.SaveCount);
    }

    [Fact]
    public async Task Request_for_a_destructive_tool_is_rejected()
    {
        await SeedTool("purge_queue", RiskLevel.Destructive, "queue.purge");

        var result = await _service.RequestApprovalAsync(
            "purge_queue", new RequestApprovalCommand(), Requester(), CancellationToken.None);

        Assert.Equal(OperationError.Validation, result.Error);
    }

    [Fact]
    public async Task Request_for_an_unregistered_tool_returns_not_found()
    {
        var result = await _service.RequestApprovalAsync(
            "missing_tool", new RequestApprovalCommand(), Requester(), CancellationToken.None);

        Assert.Equal(OperationError.NotFound, result.Error);
    }

    [Fact]
    public async Task A_second_pending_request_for_the_same_tuple_conflicts()
    {
        await SeedTool("redrive_dead_letter_queue", RiskLevel.Privileged, "queue.redrive");
        await _service.RequestApprovalAsync(
            "redrive_dead_letter_queue", new RequestApprovalCommand(), Requester(), CancellationToken.None);

        var second = await _service.RequestApprovalAsync(
            "redrive_dead_letter_queue", new RequestApprovalCommand(), Requester(), CancellationToken.None);

        Assert.Equal(OperationError.Conflict, second.Error);
    }

    [Fact]
    public async Task Approve_transitions_the_request()
    {
        var id = await OpenApproval();

        var result = await _service.ApproveAsync(id, Approver(), "ok", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(ApprovalStatus.Approved, result.Value!.Status);
        Assert.Equal("ops_admin", result.Value.DecidedBy);
    }

    [Fact]
    public async Task Lifecycle_events_are_recorded_to_the_audit_trail()
    {
        var id = await OpenApproval();

        await _service.ApproveAsync(id, Approver(), null, CancellationToken.None);

        Assert.Equal(
            [AuditEventType.ApprovalRequested, AuditEventType.ApprovalApproved],
            _audit.Approvals.Select(a => a.EventType).ToArray());
    }

    [Fact]
    public async Task Requester_cannot_approve_their_own_request()
    {
        var id = await OpenApproval();

        var result = await _service.ApproveAsync(id, Requester(), null, CancellationToken.None);

        Assert.Equal(OperationError.Validation, result.Error);
    }

    [Fact]
    public async Task Approving_an_unknown_request_returns_not_found()
    {
        var result = await _service.ApproveAsync(Guid.NewGuid(), Approver(), null, CancellationToken.None);

        Assert.Equal(OperationError.NotFound, result.Error);
    }

    [Fact]
    public async Task An_already_approved_request_cannot_be_rejected()
    {
        var id = await OpenApproval();
        await _service.ApproveAsync(id, Approver(), null, CancellationToken.None);

        var reject = await _service.RejectAsync(id, Approver(), null, CancellationToken.None);

        Assert.Equal(OperationError.Conflict, reject.Error);
    }

    [Fact]
    public async Task List_filters_by_status()
    {
        var approvedId = await OpenApproval("redrive_dead_letter_queue");
        await _service.ApproveAsync(approvedId, Approver(), null, CancellationToken.None);
        await OpenApproval("restart_worker_service");

        var pending = await _service.ListAsync(ApprovalStatus.Pending, CancellationToken.None);
        var all = await _service.ListAsync(null, CancellationToken.None);

        Assert.Equal(["restart_worker_service"], pending.Value!.Select(a => a.ToolName).ToArray());
        Assert.Equal(2, all.Value!.Count);
    }

    private async Task<Guid> OpenApproval(string tool = "redrive_dead_letter_queue")
    {
        await SeedTool(tool, RiskLevel.Privileged, "queue.redrive");
        var result = await _service.RequestApprovalAsync(
            tool, new RequestApprovalCommand(), Requester(), CancellationToken.None);
        return result.Value!.Id;
    }

    private async Task SeedTool(string name, RiskLevel riskLevel, params string[] scopes)
    {
        var spec = new ToolVersionSpec(
            ToolVersionNumber.Create("1.0"),
            "Operates on a dead-letter queue.",
            riskLevel,
            ApprovalRequired: false,
            scopes,
            TimeoutSeconds: 30,
            """{"type":"object"}""",
            """{"type":"object"}""");
        await _tools.AddAsync(ToolDefinition.Register(ToolName.Create(name), spec, Now), CancellationToken.None);
    }

    private static CallerPrincipal Requester() =>
        new(ClientId.Create("incident_agent"), IdentityType.Agent, ["queue.redrive"]);

    private static CallerPrincipal Approver() =>
        new(ClientId.Create("ops_admin"), IdentityType.User, ["gateway.admin"]);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
