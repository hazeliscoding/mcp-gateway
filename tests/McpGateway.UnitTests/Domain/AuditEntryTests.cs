using McpGateway.Domain.Auditing;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Domain;

public class AuditEntryTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_sets_all_supplied_fields()
    {
        var id = Guid.NewGuid();
        var approvalId = Guid.NewGuid();

        var entry = AuditEntry.Create(
            id, Now, "trace-123", AuditEventType.AuthorizationDecision,
            ClientId.Create("incident_agent"), IdentityType.Agent, "Permitted",
            ToolName.Create("redrive_dead_letter_queue"), ToolVersionNumber.Create("1.0"),
            detail: "Permitted", requestHash: "abc123", approvalId: approvalId);

        Assert.Equal(id, entry.Id);
        Assert.Equal(Now, entry.OccurredAt);
        Assert.Equal("trace-123", entry.TraceId);
        Assert.Equal(AuditEventType.AuthorizationDecision, entry.EventType);
        Assert.Equal("incident_agent", entry.ActorClientId.Value);
        Assert.Equal("Permitted", entry.Result);
        Assert.Equal("1.0.0", entry.Version!.ToString());
        Assert.Equal("abc123", entry.RequestHash);
        Assert.Equal(approvalId, entry.ApprovalId);
    }

    [Fact]
    public void Create_leaves_optional_fields_null_when_omitted()
    {
        var entry = AuditEntry.Create(
            Guid.NewGuid(), Now, "trace-123", AuditEventType.ApprovalApproved,
            ClientId.Create("ops_admin"), IdentityType.User, "Approved");

        Assert.Null(entry.ToolName);
        Assert.Null(entry.Version);
        Assert.Null(entry.Detail);
        Assert.Null(entry.RequestHash);
        Assert.Null(entry.ApprovalId);
    }
}
