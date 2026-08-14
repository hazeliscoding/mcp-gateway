using McpGateway.Domain.Approvals;
using McpGateway.Domain.Authorization;
using McpGateway.Domain.Tools;

namespace McpGateway.Application.Approvals;

/// <summary>
/// Opens an approval request for a tool. The requester comes from the caller's
/// token, never this body — a caller cannot request approval on another's behalf.
/// </summary>
/// <param name="Version">Target version, or <see langword="null"/> for the latest active version.</param>
/// <param name="Action">Action needing approval; defaults to invocation.</param>
/// <param name="Environment">Target environment; defaults to <c>production</c> when omitted.</param>
/// <param name="Resource">Optional resource identifier the action would touch.</param>
public sealed record RequestApprovalCommand(
    string? Version = null,
    ToolAction Action = ToolAction.Invoke,
    string? Environment = null,
    string? Resource = null);

/// <summary>Approve/reject payload; the approver comes from the token.</summary>
public sealed record DecisionRequest(string? Note = null);

/// <summary>An approval request as returned to callers.</summary>
public sealed record ApprovalResponse(
    Guid Id,
    string ToolName,
    string Version,
    string RequesterClientId,
    RiskLevel RiskLevel,
    ToolAction Action,
    string Environment,
    string? Resource,
    ApprovalStatus Status,
    DateTimeOffset RequestedAt,
    DateTimeOffset? DecidedAt,
    string? DecidedBy,
    string? DecisionNote);
