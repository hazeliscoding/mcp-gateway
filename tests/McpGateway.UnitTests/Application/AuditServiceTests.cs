using McpGateway.Application.Auditing;
using McpGateway.Application.Authorization;
using McpGateway.Domain.Approvals;
using McpGateway.Domain.Auditing;
using McpGateway.Domain.Authorization;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Application;

public class AuditServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeAuditRepository _repository = new();
    private readonly AuditService _service;

    public AuditServiceTests()
    {
        _service = new AuditService(
            _repository, new StubTraceContext("trace-abc"), new HashOnlyPayloadHasher(), new FixedTimeProvider(Now));
    }

    [Fact]
    public async Task Recording_an_authorization_stamps_trace_time_and_hashes_the_input()
    {
        await _service.RecordAuthorizationAsync(
            Caller(), ToolName.Create("redrive_dead_letter_queue"), ToolVersionNumber.Create("1.0"),
            ToolAction.Invoke, "redrive_dead_letter_queue|1.0.0|Invoke|production|arn:queue",
            AuthorizationOutcome.Denied, [AuthorizationReasonCode.MissingScopes], CancellationToken.None);

        var entry = Assert.Single(_repository.Entries);
        Assert.Equal(AuditEventType.AuthorizationDecision, entry.EventType);
        Assert.Equal("Denied", entry.Result);
        Assert.Equal("MissingScopes", entry.Detail);
        Assert.Equal("trace-abc", entry.TraceId);
        Assert.Equal(Now, entry.OccurredAt);

        // The raw resource is hashed, never stored verbatim.
        Assert.Equal(HashOnlyPayloadHasher.HashOf("redrive_dead_letter_queue|1.0.0|Invoke|production|arn:queue"), entry.RequestHash);
        Assert.DoesNotContain("arn:queue", entry.RequestHash);
    }

    [Fact]
    public async Task Recording_an_approval_links_the_approval_and_labels_the_result()
    {
        var approval = ApprovalRequest.Open(
            Guid.NewGuid(), ToolName.Create("redrive_dead_letter_queue"), ToolVersionNumber.Create("1.0"),
            ClientId.Create("incident_agent"), RiskLevel.Privileged, ToolAction.Invoke, "production", null, Now);

        await _service.RecordApprovalAsync(AuditEventType.ApprovalApproved, Caller(), approval, CancellationToken.None);

        var entry = Assert.Single(_repository.Entries);
        Assert.Equal(AuditEventType.ApprovalApproved, entry.EventType);
        Assert.Equal("Approved", entry.Result);
        Assert.Equal(approval.Id, entry.ApprovalId);
    }

    [Fact]
    public async Task List_filters_by_event_type_and_caps_the_limit()
    {
        await Seed(AuditEventType.AuthorizationDecision, "get_queue_metrics");
        await Seed(AuditEventType.ApprovalRequested, "redrive_dead_letter_queue");

        var authOnly = await _service.ListAsync(
            new AuditQueryFilter(EventType: AuditEventType.AuthorizationDecision), CancellationToken.None);
        var capped = await _service.ListAsync(new AuditQueryFilter(Limit: 10_000), CancellationToken.None);

        Assert.Equal(["get_queue_metrics"], authOnly.Value!.Select(a => a.ToolName!).ToArray());
        Assert.Equal(2, capped.Value!.Count);
    }

    [Fact]
    public async Task Stats_default_window_is_the_seven_days_ending_now()
    {
        // Just inside the window (six days back) versus just outside (eight days back).
        await SeedAt(Now.AddDays(-6), "get_queue_metrics");
        await SeedAt(Now.AddDays(-8), "old_tool");

        var stats = await _service.GetStatsAsync(new AuditStatsFilter(), CancellationToken.None);

        Assert.Equal(Now, stats.Value!.To);
        Assert.Equal(Now.AddDays(-7), stats.Value.From);
        Assert.Equal(1, stats.Value.TotalEvents);
        Assert.Equal("get_queue_metrics", Assert.Single(stats.Value.EventsByTool).Name);
    }

    [Fact]
    public async Task Stats_reject_an_inverted_window()
    {
        var result = await _service.GetStatsAsync(
            new AuditStatsFilter(From: Now, To: Now.AddDays(-1)), CancellationToken.None);

        Assert.False(result.IsSuccess);
    }

    private Task SeedAt(DateTimeOffset occurredAt, string tool) =>
        _repository.AddAsync(
            AuditEntry.Create(
                Guid.NewGuid(), occurredAt, "trace-abc", AuditEventType.AuthorizationDecision,
                ClientId.Create("incident_agent"), IdentityType.Agent, "Permitted",
                ToolName.Create(tool), ToolVersionNumber.Create("1.0"), detail: null, requestHash: "h:0"),
            CancellationToken.None);

    private Task Seed(AuditEventType eventType, string tool) =>
        eventType == AuditEventType.AuthorizationDecision
            ? _service.RecordAuthorizationAsync(
                Caller(), ToolName.Create(tool), ToolVersionNumber.Create("1.0"), ToolAction.Invoke,
                $"{tool}|1.0.0|Invoke|production|-", AuthorizationOutcome.Permitted, [], CancellationToken.None)
            : _service.RecordApprovalAsync(eventType, Caller(), ApprovalRequest.Open(
                Guid.NewGuid(), ToolName.Create(tool), ToolVersionNumber.Create("1.0"),
                ClientId.Create("incident_agent"), RiskLevel.Privileged, ToolAction.Invoke, "production", null, Now),
                CancellationToken.None);

    private static CallerPrincipal Caller() =>
        new(ClientId.Create("incident_agent"), IdentityType.Agent, ["queue.read"]);

    private sealed class StubTraceContext(string traceId) : ITraceContext
    {
        public string CurrentTraceId { get; } = traceId;
    }

    private sealed class HashOnlyPayloadHasher : IPayloadHasher
    {
        public string Hash(string value) => HashOf(value);

        public static string HashOf(string value) => $"h:{value.GetHashCode():x8}";
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
