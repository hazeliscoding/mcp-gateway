import { RiskLevel, ToolVersionStatus } from './enums';

export interface ToolSummaryResponse {
  name: string;
  enabled: boolean;
  latestVersion: string;
  description: string;
  riskLevel: RiskLevel;
  approvalRequired: boolean;
  createdAt: string;
}

export interface ToolVersionResponse {
  version: string;
  description: string;
  riskLevel: RiskLevel;
  approvalRequired: boolean;
  requiredScopes: string[];
  timeoutSeconds: number;
  inputSchema: unknown;
  outputSchema: unknown;
  status: ToolVersionStatus;
  registeredAt: string;
}

export interface ToolDetailResponse {
  name: string;
  enabled: boolean;
  createdAt: string;
  versions: ToolVersionResponse[];
}

export interface RegisterToolRequest {
  name: string;
  version: string;
  description: string;
  riskLevel: RiskLevel;
  approvalRequired: boolean;
  requiredScopes: string[];
  timeoutSeconds: number;
  inputSchema: unknown;
  outputSchema: unknown;
}

export type RegisterVersionRequest = Omit<RegisterToolRequest, 'name'>;

export interface ToolListFilter {
  riskLevel?: RiskLevel;
  includeDisabled?: boolean;
  nameContains?: string;
}
