# ADR 0005 — Risk classification drives the authorization outcome

**Status:** Accepted (2026-08-14)

## Context

Phase 3 produced a binary permit/deny decision. Every tool version already carries
a `RiskLevel` (`ReadOnly`/`Write`/`Privileged`/`Destructive`) and an
`ApprovalRequired` flag, but the engine ignored both. Phase 4 makes risk drive the
outcome per the plan's matrix: read-only and write run automatically, privileged
needs approval, and destructive is prohibited or (later) multi-party approved.

## Decision

The authorization outcome becomes four-valued —
`AuthorizationOutcome { Permitted, RequiresApproval, Denied, Prohibited }` — and
`AuthorizationPolicy.Evaluate` gains a final risk step that runs **only for the
`Invoke` action and only after** the existing kill-switch, version, deprecation, and
scope rules. Ordering matters: a caller lacking the required scopes is `Denied`, never
asked to seek approval for something they could not run anyway.

The risk step:

| Risk | Outcome (for `Invoke`) |
|------|------------------------|
| ReadOnly | `Permitted` |
| Write | `Permitted` (its "depends on scope" is already enforced by the scope check) |
| Privileged, or any version with `ApprovalRequired` | `RequiresApproval` |
| Destructive | `Prohibited` |

`Discover` is never risk-gated — reading a tool's metadata is safe regardless of class.

## Consequences

- `RequiresApproval` and `Prohibited` are still successful *evaluations*: the
  `/authorize` endpoint returns HTTP 200 with the `outcome`, consistent with ADR 0004.
  `Permit` remains on the response as a convenience but is `false` for both — callers
  should branch on `outcome`, not `permit`.
- `Destructive` is `Prohibited` rather than routed to multi-party approval because that
  workflow does not exist yet; a categorical block is the conservative default and the
  documented upgrade path.
- The `ApprovalRequired` registration flag lets a lower-risk tool opt into approval
  without being reclassified as privileged.
- This phase is classification only. Making `RequiresApproval` actionable — an
  `ApprovalRequest` aggregate, approve/reject endpoints, and re-authorizing an approved
  call — is the next phase (the "Approval Engine"). The reason codes
  (`ApprovalRequired`, `RiskProhibited`) are already structured for the audit trail
  (Phase 5) and console (Phase 6) to consume.
