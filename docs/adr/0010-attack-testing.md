# ADR 0010 — Attack testing as executable red-team scenarios

**Status:** Accepted (2026-08-19)

## Context

Phases 1–6 built the gateway's controls: token auth, scope-based authorization,
risk classification, four-eyes approvals, an append-only audit trail, and an admin
console. Phase 7 asks whether those controls actually hold under attack, across the
list in `docs/PLAN.md`: prompt injection in tool results, privilege escalation, tool
spoofing, version downgrade, parameter injection, oversized responses, secret leakage,
and cross-tenant access.

A complication: **the gateway authorizes tool calls but does not execute them.** There
is no invocation or proxy endpoint yet, so two of the listed attacks — prompt injection
*in tool results* and *oversized responses* — have no surface to target. Inventing an
execution layer just to attack it would be speculative and belongs with the real
execution phase.

## Decision

Encode each testable attack as an **executable red-team scenario** — an integration
test named for the attack, asserting the control holds — grouped under
`tests/McpGateway.IntegrationTests/Security/`. These run in the normal `dotnet test`
suite against a real Postgres (Testcontainers), so a regression that reopens a hole
fails the build.

Coverage this phase:

- **Privilege escalation** — body-injected scopes ignored; forged (wrong-key),
  expired, and tampered tokens rejected; agents cannot self-grant admin or decide
  approvals (`PrivilegeEscalationTests`).
- **Version downgrade** — deprecated versions denied explicitly and never resolved
  implicitly by "latest" (`VersionDowngradeTests`).
- **Tool spoofing** — no silent re-registration over an existing name; non-operators
  cannot register; unknown tools are not found (`ToolSpoofingTests`).
- **Cross-identity grant isolation** — one caller's approval never permits another
  (`GrantIsolationTests`). This is the single-tenant analogue of cross-tenant access.
- **Parameter injection** — injection payloads in filters match nothing; malformed and
  oversized identifiers are 4xx, not crashes; hostile `resource` values are opaque and
  hashed (`ParameterInjectionTests`).
- **Secret leakage** — secrets appear only at issue, never on reads; no hash material is
  projected; token failures are uniform `invalid_client` (`SecretLeakageTests`).

One hardening change accompanies the tests: **audit reads now require `gateway.admin`**
(previously any authenticated caller). The trail spans every identity's activity, so
exposing it to a low-privilege agent is itself an information-disclosure and
cross-identity concern. This resolves the open question left in ADR 0008.

**Deferred (documented, not silently omitted):** prompt injection in tool results and
oversized-response handling require the execution/proxy layer that does not exist yet.
They are recorded in `docs/THREAT-MODEL.md` as open items to be enforced and tested when
tool invocation is built, alongside the untrusted-output handling the master plan calls
for.

## Consequences

- The attack list becomes a living regression suite rather than a one-time audit.
- The threat model is explicit about what is *not* yet covered, so "no test" is never
  mistaken for "no risk."
- Tightening audit reads is a small breaking change for any non-admin caller that read
  the trail; agents never needed it, and the console signs in as an operator.
