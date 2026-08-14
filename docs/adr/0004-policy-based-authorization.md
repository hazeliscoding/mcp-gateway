# ADR 0004 — Deterministic policy-based authorization

**Status:** Accepted (2026-08-14)

## Context

Phase 3 must decide whether a given principal may act on a given tool before any
execution. The plan's model is attribute-based (ABAC): evaluate the caller, tool,
environment, resource, and action together. Two questions had to be settled: where
the rules live, and what the callable surface is given that no tool execution path
exists yet (execution/proxying comes with later phases and the flagship).

## Decision

Authorization rules live in a pure domain engine, `AuthorizationPolicy.Evaluate`,
that maps an `AuthorizationRequest` (the ABAC attributes) to an
`AuthorizationDecision` (permit/deny plus machine-readable reason codes). It is a
deterministic function with no I/O. The application `AuthorizationService` only
loads the tool and resolves the target version, then delegates the verdict to the
engine; the API layer maps the outcome to HTTP.

Phase 3 enforces three rules in precedence order: the tool-level kill switch
(disabled → deny), version lifecycle (missing or deprecated version → deny for
invocation), and scope coverage (the caller must hold every scope the version
requires). Environment and resource are carried on the request for audit and for
future rules but add no denial this phase; risk-class → approval gating is Phase 4
and will slot in as an additional rule in the same engine.

The callable surface is a decision endpoint, `POST /api/tools/{name}/authorize`,
which returns the verdict (HTTP 200 for both permit and deny — evaluating is what
succeeded). It is the gate that real tool execution will call through later.

## Consequences

- Business rules stay deterministic and unit-testable in isolation, honoring the
  project rule that models never decide their own permissions and controllers hold
  no business logic. ASP.NET policy handlers were rejected for the fine-grained
  decision because they would scatter the ABAC rules across attributes and handlers.
- The caller's scopes come only from the validated token (projected by
  `ClaimsPrincipalExtensions.ToCallerPrincipal`); the request body carries only
  *what* is being asked about, never *who* the caller is or what they hold. Scopes
  placed in the body are ignored — an integration test locks this in as a
  privilege-escalation guard (previewing Phase 7).
- Reason codes (not just messages) mean the Phase 5 audit trail and the Phase 6
  console can act on structured outcomes.
- Returning 200 on a deny keeps "you may not call authorize" (401/403) distinct from
  "the action you asked about is denied" (200 with `permit: false`). When an
  enforcing execution endpoint arrives, it will translate a deny into a real 403.
