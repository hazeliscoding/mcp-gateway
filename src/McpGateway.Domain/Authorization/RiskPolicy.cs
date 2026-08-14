using McpGateway.Domain.Tools;

namespace McpGateway.Domain.Authorization;

/// <summary>How a tool version's risk class dispositions an invocation.</summary>
public enum RiskDisposition
{
    /// <summary>Runs automatically once access is granted.</summary>
    Automatic,

    /// <summary>Allowed in principle, but a human must approve first.</summary>
    RequiresApproval,

    /// <summary>Categorically barred from execution through the gateway.</summary>
    Prohibited,
}

/// <summary>
/// The single place the risk matrix lives, shared by the authorization engine and
/// the approval workflow so both agree on what a risk class means. ReadOnly and
/// Write are automatic (Write's scope requirement is enforced separately); a
/// Privileged class — or any version explicitly flagged for approval — needs human
/// sign-off; Destructive is prohibited (multi-party approval is the deferred
/// alternative).
/// </summary>
public static class RiskPolicy
{
    public static RiskDisposition Classify(RiskLevel riskLevel, bool approvalRequired)
    {
        if (riskLevel == RiskLevel.Destructive)
        {
            return RiskDisposition.Prohibited;
        }

        if (riskLevel == RiskLevel.Privileged || approvalRequired)
        {
            return RiskDisposition.RequiresApproval;
        }

        return RiskDisposition.Automatic;
    }
}
