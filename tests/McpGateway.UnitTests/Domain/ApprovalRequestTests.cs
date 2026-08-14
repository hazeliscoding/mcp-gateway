using McpGateway.Domain;
using McpGateway.Domain.Approvals;
using McpGateway.Domain.Authorization;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Domain;

public class ApprovalRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = Now.AddMinutes(5);
    private static readonly ClientId Requester = ClientId.Create("incident_agent");
    private static readonly ClientId Approver = ClientId.Create("ops_admin");

    [Fact]
    public void Open_creates_a_pending_request()
    {
        var approval = Open();

        Assert.Equal(ApprovalStatus.Pending, approval.Status);
        Assert.Equal(Requester, approval.RequesterClientId);
        Assert.Equal(Now, approval.RequestedAt);
        Assert.Null(approval.DecidedAt);
        Assert.Null(approval.DecidedBy);
    }

    [Fact]
    public void Approve_transitions_to_approved_and_records_the_approver()
    {
        var approval = Open();

        approval.Approve(Approver, "looks safe", Later);

        Assert.Equal(ApprovalStatus.Approved, approval.Status);
        Assert.Equal(Approver, approval.DecidedBy);
        Assert.Equal(Later, approval.DecidedAt);
        Assert.Equal("looks safe", approval.DecisionNote);
    }

    [Fact]
    public void Reject_transitions_to_rejected()
    {
        var approval = Open();

        approval.Reject(Approver, null, Later);

        Assert.Equal(ApprovalStatus.Rejected, approval.Status);
        Assert.Equal(Approver, approval.DecidedBy);
        Assert.Null(approval.DecisionNote);
    }

    [Fact]
    public void Requester_cannot_approve_their_own_request()
    {
        var approval = Open();

        Assert.Throws<DomainRuleException>(() => approval.Approve(Requester, null, Later));
    }

    [Fact]
    public void A_decided_request_cannot_be_decided_again()
    {
        var approval = Open();
        approval.Approve(Approver, null, Later);

        Assert.Throws<DomainConflictException>(() => approval.Approve(Approver, null, Later));
        Assert.Throws<DomainConflictException>(() => approval.Reject(Approver, null, Later));
    }

    [Fact]
    public void Decision_note_over_500_characters_is_rejected()
    {
        var approval = Open();

        Assert.Throws<DomainRuleException>(() => approval.Approve(Approver, new string('x', 501), Later));
    }

    private static ApprovalRequest Open() =>
        ApprovalRequest.Open(
            Guid.NewGuid(),
            ToolName.Create("redrive_dead_letter_queue"),
            ToolVersionNumber.Create("1.0"),
            Requester,
            RiskLevel.Privileged,
            ToolAction.Invoke,
            environment: "production",
            resource: null,
            Now);
}
