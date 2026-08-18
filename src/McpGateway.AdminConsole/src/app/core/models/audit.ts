import { AuditEventType } from './enums';

export interface AuditEntryResponse {
  id: string;
  occurredAt: string;
  traceId: string;
  eventType: AuditEventType;
  actorClientId: string;
  result: string;
  toolName?: string;
  version?: string;
  detail?: string;
  requestHash?: string;
  approvalId?: string;
}

export interface AuditQueryFilter {
  toolName?: string;
  actor?: string;
  eventType?: AuditEventType;
  from?: string;
  to?: string;
  limit?: number;
}

export interface NamedCount {
  name: string;
  count: number;
}

export interface DailyCount {
  date: string;
  count: number;
}

export interface AuditStatsResponse {
  from: string;
  to: string;
  totalEvents: number;
  eventsByType: NamedCount[];
  eventsByTool: NamedCount[];
  authorizationOutcomes: NamedCount[];
  eventsByActor: NamedCount[];
  eventsPerDay: DailyCount[];
}
