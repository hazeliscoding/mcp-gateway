using McpGateway.Domain.Authorization;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;

namespace McpGateway.UnitTests.Domain;

public class AuthorizationPolicyTests
{
    private static readonly ClientId Caller = ClientId.Create("incident_agent");

    [Fact]
    public void Permits_when_caller_holds_all_required_scopes()
    {
        var version = VersionRequiring("queue.read", "queue.redrive");
        var request = Request(version, granted: ["queue.read", "queue.redrive"]);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.True(decision.Permit);
        Assert.Equal(AuthorizationReasonCode.Permitted, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void Permits_when_caller_holds_a_superset_of_required_scopes()
    {
        var version = VersionRequiring("queue.read");
        var request = Request(version, granted: ["queue.read", "queue.redrive", "gateway.admin"]);

        Assert.True(AuthorizationPolicy.Evaluate(request).Permit);
    }

    [Fact]
    public void Denies_and_lists_only_the_missing_scopes()
    {
        var version = VersionRequiring("queue.read", "queue.redrive");
        var request = Request(version, granted: ["queue.read"]);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.False(decision.Permit);
        var reason = Assert.Single(decision.Reasons);
        Assert.Equal(AuthorizationReasonCode.MissingScopes, reason.Code);
        Assert.Contains("queue.redrive", reason.Message);
        Assert.DoesNotContain("queue.read,", reason.Message);
    }

    [Fact]
    public void Disabled_tool_denies_before_any_scope_check()
    {
        // Caller holds every scope, but the kill switch must still win.
        var version = VersionRequiring("queue.read");
        var request = Request(version, granted: ["queue.read"], toolEnabled: false);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.False(decision.Permit);
        Assert.Equal(AuthorizationReasonCode.ToolDisabled, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void Missing_version_denies_with_version_not_found()
    {
        var request = Request(version: null, granted: ["queue.read"]);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.False(decision.Permit);
        Assert.Equal(AuthorizationReasonCode.VersionNotFound, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void Deprecated_version_cannot_be_invoked()
    {
        var version = DeprecatedVersionRequiring("queue.read");
        var request = Request(version, granted: ["queue.read"], action: ToolAction.Invoke);

        var decision = AuthorizationPolicy.Evaluate(request);

        Assert.False(decision.Permit);
        Assert.Equal(AuthorizationReasonCode.VersionDeprecated, Assert.Single(decision.Reasons).Code);
    }

    [Fact]
    public void Deprecated_version_can_still_be_discovered()
    {
        var version = DeprecatedVersionRequiring("queue.read");
        var request = Request(version, granted: ["queue.read"], action: ToolAction.Discover);

        Assert.True(AuthorizationPolicy.Evaluate(request).Permit);
    }

    private static ToolVersion VersionRequiring(params string[] scopes) =>
        TestSpecs.RegisteredToolWith(TestSpecs.Valid(scopes: scopes)).LatestVersion;

    private static ToolVersion DeprecatedVersionRequiring(params string[] scopes)
    {
        var tool = TestSpecs.RegisteredToolWith(TestSpecs.Valid(scopes: scopes));
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
