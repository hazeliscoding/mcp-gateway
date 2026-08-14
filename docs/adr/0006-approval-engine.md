# ADR 0006 — Approval engine for risk-gated invocations

**Status:** Accepted (2026-08-14)

## Context

Phase 4 classifies privileged invocations as `RequiresApproval`, but nothing could
act on it — there was no way to record a request, have a human approve it, and let
the call proceed. This is the "Approval Engine" from the architecture, closing the
demo's block → approve → permit loop. It sits between Phase 4 and the audit trail.

## Decision

A new `ApprovalRequest` aggregate records one human-in-the-loop decision for a
`(requester, tool, version)` tuple. The workflow is explicit and separate from the
authorization query:

- `POST /api/tools/{name}/approvals` opens a Pending request. The requester comes
  from the token; a request is only accepted for a version whose `RiskPolicy`
  disposition is `RequiresApproval` (automatic and destructive versions are refused),
  and a second Pending request for the same tuple conflicts.
- `POST /api/approvals/{id}/approve` / `/reject` decide it. **Four-eyes is a domain
  invariant**: the approver may not be the requester.
- `/authorize` stays a **read-only query**. It consults the approval store and feeds
  a single `ApprovalGranted` boolean into the engine; an approved grant upgrades the
  outcome from `RequiresApproval` to `Permitted`. The engine remains the only place
  decision rules live.

The risk matrix that both authorization and approval consult was extracted into
`RiskPolicy.Classify` so the two never drift.

## Consequences

- Grants are **standing**, keyed to `(requester, tool, version)`: once approved,
  re-authorization is permitted until a real execution path can consume or expire the
  grant. Single-use consumption and expiry are deferred to the execution phase — until
  then, an approval is a reusable grant, which is called out as a known simplification.
- Four-eyes prevents self-approval, but any *other* authenticated principal can approve.
  Restricting approval to operators via a dedicated scope is deferred: no management
  endpoint is scope-gated yet, so gating only approvals would be inconsistent — it will
  come with a broader management-authorization pass.
- `Destructive` remains prohibited; approving it is explicitly refused, consistent with
  ADR 0005. Multi-party approval stays the deferred alternative.
- The aggregate snapshots `RiskLevel` and stamps `DecidedBy`/`DecidedAt`, so the Phase 5
  audit trail and the Phase 6 console have the full decision record without extra joins.
