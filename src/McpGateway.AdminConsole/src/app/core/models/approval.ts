import { ApprovalStatus, RiskLevel, ToolAction } from './enums';

export interface ApprovalResponse {
  id: string;
  toolName: string;
  version: string;
  requesterClientId: string;
  riskLevel: RiskLevel;
  action: ToolAction;
  environment: string;
  resource?: string;
  status: ApprovalStatus;
  requestedAt: string;
  decidedAt?: string;
  decidedBy?: string;
  decisionNote?: string;
}

export interface DecisionRequest {
  note?: string;
}
