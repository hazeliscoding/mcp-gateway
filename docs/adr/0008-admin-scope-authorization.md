# ADR 0008 — Admin-scope authorization on management endpoints

**Status:** Accepted (2026-08-18)

## Context

Every protected endpoint used a bare `RequireAuthorization()`, so any authenticated
identity — including a low-privilege agent — could register tools, disable them,
manage identities, or decide approvals. Scopes were enforced only *inside* the tool
authorization decision (a caller's granted scopes versus a tool's required scopes),
never at the management surface. The Phase 6 admin console makes this gap concrete: it
authenticates as an operator and calls exactly these endpoints, so the boundary between
"operator" and "agent" now has to be real.

## Decision

Introduce a single `gateway.admin` scope and an `AdminScope` authorization policy that
asserts on the space-delimited `scope` claim. `RequireClaim` cannot express this because
scopes are issued as one space-delimited claim value (see `JwtTokenIssuer`), so the
policy splits the claim and checks membership.

The policy is applied to the operator surface and withheld from the surface agents use
themselves:

- **Operator-only (`gateway.admin`):** all identity management; tool registry mutations
  (register, add version, enable, disable, deprecate); approval decisions (approve,
  reject).
- **Any authenticated caller:** tool discovery (`GET /api/tools`, `GET /api/tools/{name}`),
  authorization decisions (`/authorize`), approval requests and reads, and the audit
  read endpoints — agents legitimately discover tools, ask whether they may act, open
  approval requests, and poll status.

Identity **reads** are operator-only too, not just mutations: agents have no reason to
enumerate other identities, and the console's permissions view is itself an operator view.

The bootstrap identity is seeded with `gateway.admin`, so a fresh `docker compose up`
has a working operator out of the box.

## Consequences

- Endpoint authorization now has two tiers instead of one. A missing scope yields `403`
  (authenticated but not permitted), distinct from `401` (unauthenticated).
- Four-eyes on approvals still lives in the domain: the admin-scope policy governs *who
  may decide*, while the `ApprovalRequest` aggregate still forbids the requester and the
  approver being the same principal. Both are exercised by tests.
- Audit reads (`GET /api/audit`, `/api/audit/stats`) remain open to any authenticated
  caller for now. Whether an agent should see the full trail is a Phase 7 (attack
  testing) question; tightening it later is a one-line change.
- The single flat `gateway.admin` scope is deliberately coarse. Finer operator roles
  (registry-admin vs. approver vs. auditor) are deferred until there is a real need — no
  speculative role hierarchy.
