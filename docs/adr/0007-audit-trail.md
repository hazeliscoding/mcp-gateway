# ADR 0007 — Append-only audit trail

**Status:** Accepted (2026-08-14)

## Context

The gateway makes real security decisions (authorize) and approval transitions but
kept no durable record of them. Phase 5 adds an audit trail so every decision is
reconstructable — who acted, on what, the result, a correlating trace id, and a hash
of the request context — backing the demo's "full audit history is visible" step and
the Phase 6 console.

## Decision

An append-only `AuditEntry` records one security event. This phase captures the
security-decision surface that exists today: **every authorization evaluation**
(`AuthorizationDecision`) and **every approval lifecycle event** (`ApprovalRequested`,
`ApprovalApproved`, `ApprovalRejected`). Tool-invocation audit with output hashes
plugs in when execution exists; admin/management-action auditing is deferred.

Capture is **service-level**: `AuthorizationService` and `ApprovalService` depend on
an `IAuditTrail` and record after producing their result, where the structured
outcome data lives. Recording stamps the entry with `TimeProvider` and the current
trace id, and saves its own row.

- **Trace id** comes from `Activity.Current` — ASP.NET Core starts a per-request
  activity even without an OpenTelemetry SDK, so a real correlation id is available;
  a random id is the fallback outside a request. A full OTel exporter is deferred.
- **Redaction**: the request context (which can include a sensitive `resource`) is
  stored as a SHA-256 `RequestHash`, never raw. Only non-sensitive fields (tool,
  version, outcome, reason codes) are stored in the clear.
- **Query**: `GET /api/audit` filters by tool, actor, event type, and time window,
  newest-first, with a capped limit.

## Consequences

- `/authorize` now performs an audit write in addition to being a domain-read-only
  decision. This is intentional: auditing is observability, not domain-state mutation
  — the read-only property (ADR 0004/0006) was about not mutating tools/identities/
  approvals, which still holds.
- The audit row is saved separately from the primary operation, so it is best-effort
  relative to that operation (an approval could persist while its audit write fails).
  Enlisting both in one transaction — and tamper-evidence/signing — are deferred.
- Reason codes and `DecidedBy`/approval links are captured, so the console (Phase 6)
  and any replay tooling have the full decision record without extra joins.
- Denials and blocked privileged attempts are recorded, which is exactly what a
  security audit needs and what the attack-testing phase (Phase 7) will assert on.
