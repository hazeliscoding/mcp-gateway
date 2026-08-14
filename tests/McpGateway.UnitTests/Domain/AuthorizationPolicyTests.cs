using McpGateway.Domain.Authorization;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Domain;

public class AuthorizationPolicyTests
{
    private static readonly ClientId Caller = ClientId.Create("incident_agent");

    [Fact]
    public void Permits_read_only_invocation_when_caller_holds_all_required_scopes()
    {
        var version = Version(RiskLevel.ReadOnly, scopes: ["queue.read", "queue.redrive"]);
        var request = Request(version, granted: ["queue.read", "queue.redrive"]);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.Equal(AuthorizationOutcome.Permitted, decision.Outcome);
        Assert.True(decision.Permit);
        Assert.Equal(AuthorizationReasonCode.Permitted, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void Permits_write_invocation_once_scopes_are_held()
    {
        var version = Version(RiskLevel.Write, scopes: ["queue.write"]);
        var request = Request(version, granted: ["queue.write"]);

        Assert.Equal(AuthorizationOutcome.Permitted, AuthorizationPolicy.Evaluate(request).Outcome);
    }

    [Fact]
    public void Permits_when_caller_holds_a_superset_of_required_scopes()
    {
        var version = Version(RiskLevel.ReadOnly, scopes: ["queue.read"]);
        var request = Request(version, granted: ["queue.read", "queue.redrive", "gateway.admin"]);

        Assert.True(AuthorizationPolicy.Evaluate(request).Permit);
    }

    [Fact]
    public void Denies_and_lists_only_the_missing_scopes()
    {
        var version = Version(RiskLevel.ReadOnly, scopes: ["queue.read", "queue.redrive"]);
        var request = Request(version, granted: ["queue.read"]);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.Equal(AuthorizationOutcome.Denied, decision.Outcome);
        var reason = Assert.Single(decision.Reasons);
        Assert.Equal(AuthorizationReasonCode.MissingScopes, reason.Code);
        Assert.Contains("queue.redrive", reason.Message);
        Assert.DoesNotContain("queue.read,", reason.Message);
    }

    [Fact]
    public void Disabled_tool_denies_before_any_scope_or_risk_check()
    {
        // Caller holds every scope, but the kill switch must still win.
        var version = Version(RiskLevel.ReadOnly, scopes: ["queue.read"]);
        var request = Request(version, granted: ["queue.read"], toolEnabled: false);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.Equal(AuthorizationOutcome.Denied, decision.Outcome);
        Assert.Equal(AuthorizationReasonCode.ToolDisabled, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void Missing_version_denies_with_version_not_found()
    {
        var request = Request(version: null, granted: ["queue.read"]);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.Equal(AuthorizationOutcome.Denied, decision.Outcome);
        Assert.Equal(AuthorizationReasonCode.VersionNotFound, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void Deprecated_version_cannot_be_invoked()
    {
        var version = DeprecatedVersion(RiskLevel.ReadOnly, scopes: ["queue.read"]);
        var request = Request(version, granted: ["queue.read"], action: ToolAction.Invoke);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.Equal(AuthorizationOutcome.Denied, decision.Outcome);
        Assert.Equal(AuthorizationReasonCode.VersionDeprecated, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void Deprecated_version_can_still_be_discovered()
    {
        var version = DeprecatedVersion(RiskLevel.ReadOnly, scopes: ["queue.read"]);
        var request = Request(version, granted: ["queue.read"], action: ToolAction.Discover);

        Assert.True(AuthorizationPolicy.Evaluate(request).Permit);
    }

    [Fact]
    public void Privileged_invocation_requires_approval()
    {
        var version = Version(RiskLevel.Privileged, scopes: ["queue.read"]);
        var request = Request(version, granted: ["queue.read"]);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.Equal(AuthorizationOutcome.RequiresApproval, decision.Outcome);
        Assert.False(decision.Permit);
        Assert.Equal(AuthorizationReasonCode.ApprovalRequired, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void Approval_flag_forces_approval_even_for_a_write_tool()
    {
        var version = Version(RiskLevel.Write, scopes: ["queue.write"], approvalRequired: true);
        var request = Request(version, granted: ["queue.write"]);

        Assert.Equal(AuthorizationOutcome.RequiresApproval, AuthorizationPolicy.Evaluate(request).Outcome);
    }

    [Fact]
    public void Destructive_invocation_is_prohibited()
    {
        var version = Version(RiskLevel.Destructive, scopes: ["queue.purge"]);
        var request = Request(version, granted: ["queue.purge"]);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.Equal(AuthorizationOutcome.Prohibited, decision.Outcome);
        Assert.Equal(AuthorizationReasonCode.RiskProhibited, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void Missing_scopes_deny_before_risk_is_classified()
    {
        // Privileged would otherwise require approval, but the caller lacks the scope entirely.
        var version = Version(RiskLevel.Privileged, scopes: ["queue.read", "queue.redrive"]);
        var request = Request(version, granted: ["queue.read"]);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.Equal(AuthorizationOutcome.Denied, decision.Outcome);
        Assert.Equal(AuthorizationReasonCode.MissingScopes, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void Risk_does_not_gate_discovery_of_a_destructive_tool()
    {
        var version = Version(RiskLevel.Destructive, scopes: ["queue.purge"]);
        var request = Request(version, granted: ["queue.purge"], action: ToolAction.Discover);

        Assert.True(AuthorizationPolicy.Evaluate(request).Permit);
    }

    private static ToolVersion Version(RiskLevel riskLevel, string[] scopes, bool approvalRequired = false) =>
        TestSpecs.RegisteredToolWith(TestSpecs.Valid(
            riskLevel: riskLevel, approvalRequired: approvalRequired, scopes: scopes)).LatestVersion;

    private static ToolVersion DeprecatedVersion(RiskLevel riskLevel, string[] scopes)
    {
        var tool = TestSpecs.RegisteredToolWith(TestSpecs.Valid(
            riskLevel: riskLevel, approvalRequired: false, scopes: scopes));
        tool.DeprecateVersion(tool.LatestVersion.Number);
        return tool.LatestVersion;
    }

    private static AuthorizationRequest Request(
        ToolVersion? version,
        IReadOnlyList<string> granted,
        bool toolEnabled = true,
        ToolAction action = ToolAction.Invoke) =>
        new(
            Caller,
            IdentityType.Agent,
            granted,
            ToolName.Create("redrive_dead_letter_queue"),
            toolEnabled,
            version,
            action,
            Environment: "production",
            Resource: null);
}
