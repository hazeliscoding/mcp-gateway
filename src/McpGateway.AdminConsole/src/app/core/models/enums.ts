// String-literal unions mirroring the gateway's C# enums, which serialize as
// strings (JsonStringEnumConverter). Kept in one place so the UI and API models
// share a single source of truth.

export type RiskLevel = 'ReadOnly' | 'Write' | 'Privileged' | 'Destructive';
export const RISK_LEVELS: RiskLevel[] = ['ReadOnly', 'Write', 'Privileged', 'Destructive'];

export type ToolVersionStatus = 'Active' | 'Deprecated';

export type IdentityType = 'User' | 'Agent' | 'Service';
export const IDENTITY_TYPES: IdentityType[] = ['User', 'Agent', 'Service'];

export type ApprovalStatus = 'Pending' | 'Approved' | 'Rejected';
export const APPROVAL_STATUSES: ApprovalStatus[] = ['Pending', 'Approved', 'Rejected'];

export type ToolAction = 'Invoke' | 'Discover';

export type AuthorizationOutcome = 'Permitted' | 'RequiresApproval' | 'Denied' | 'Prohibited';

export type AuthorizationReasonCode =
  | 'Permitted'
  | 'ToolDisabled'
  | 'VersionNotFound'
  | 'VersionDeprecated'
  | 'MissingScopes'
  | 'ApprovalRequired'
  | 'RiskProhibited';

export type AuditEventType =
  | 'AuthorizationDecision'
  | 'ApprovalRequested'
  | 'ApprovalApproved'
  | 'ApprovalRejected';
export const AUDIT_EVENT_TYPES: AuditEventType[] = [
  'AuthorizationDecision',
  'ApprovalRequested',
  'ApprovalApproved',
  'ApprovalRejected',
];

/** Scope that grants operator (admin) access to management endpoints. */
export const GATEWAY_ADMIN_SCOPE = 'gateway.admin';
