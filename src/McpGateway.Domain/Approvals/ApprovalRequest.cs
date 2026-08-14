using McpGateway.Domain.Authorization;
using McpGateway.Domain.Identities;
using McpGateway.Domain.Tools;

namespace McpGateway.Domain.Approvals;

/// <summary>
/// Aggregate root for one human-in-the-loop approval of a risk-gated tool
/// invocation. Opened when a requester needs sign-off for a privileged (or
/// approval-flagged) tool version; a different principal then approves or rejects
/// it. The id and timestamps are supplied by the caller so the aggregate stays
/// deterministic.
/// </summary>
public sealed class ApprovalRequest
{
    private const int MaxNoteLength = 500;

    public Guid Id { get; private set; }
    public ToolName ToolName { get; private set; } = null!;
    public ToolVersionNumber Version { get; private set; } = null!;

    /// <summary>The principal that needs approval to run the tool.</summary>
    public ClientId RequesterClientId { get; private set; } = null!;

    /// <summary>Risk class captured when the request was opened.</summary>
    public RiskLevel RiskLevel { get; private set; }

    public ToolAction Action { get; private set; }
    public string Environment { get; private set; } = null!;
    public string? Resource { get; private set; }
    public ApprovalStatus Status { get; private set; }
    public DateTimeOffset RequestedAt { get; private set; }

    /// <summary>When the request was approved or rejected; null while pending.</summary>
    public DateTimeOffset? DecidedAt { get; private set; }

    /// <summary>Who decided; null while pending. Never equal to the requester.</summary>
    public ClientId? DecidedBy { get; private set; }

    public string? DecisionNote { get; private set; }

    private ApprovalRequest()
    {
        // EF Core materialization only.
    }

    public static ApprovalRequest Open(
        Guid id,
        ToolName toolName,
        ToolVersionNumber version,
        ClientId requesterClientId,
        RiskLevel riskLevel,
        ToolAction action,
        string environment,
        string? resource,
        DateTimeOffset utcNow) =>
        new()
        {
            Id = id,
            ToolName = toolName,
            Version = version,
            RequesterClientId = requesterClientId,
            RiskLevel = riskLevel,
            Action = action,
            Environment = environment,
            Resource = resource,
            Status = ApprovalStatus.Pending,
            RequestedAt = utcNow,
        };

    /// <summary>
    /// Approves the request. Enforces four-eyes: the approver may not be the
    /// requester.
    /// </summary>
    /// <exception cref="DomainConflictException">The request has already been decided.</exception>
    /// <exception cref="DomainRuleException">The approver is the requester.</exception>
    public void Approve(ClientId approver, string? note, DateTimeOffset utcNow)
    {
        if (approver == RequesterClientId)
        {
            throw new DomainRuleException("An approval request cannot be approved by its requester.");
        }

        Decide(ApprovalStatus.Approved, approver, note, utcNow);
    }

    /// <exception cref="DomainConflictException">The request has already been decided.</exception>
    public void Reject(ClientId approver, string? note, DateTimeOffset utcNow) =>
        Decide(ApprovalStatus.Rejected, approver, note, utcNow);

    private void Decide(ApprovalStatus status, ClientId approver, string? note, DateTimeOffset utcNow)
    {
        if (Status != ApprovalStatus.Pending)
        {
            throw new DomainConflictException($"Approval request {Id} is already {Status.ToString().ToLowerInvariant()}.");
        }

        var trimmedNote = note?.Trim();
        if (trimmedNote is { Length: > MaxNoteLength })
        {
            throw new DomainRuleException($"Decision note must be at most {MaxNoteLength} characters.");
        }

        Status = status;
        DecidedBy = approver;
        DecisionNote = string.IsNullOrEmpty(trimmedNote) ? null : trimmedNote;
        DecidedAt = utcNow;
    }
}
