# ADR 0009 — Angular admin console

**Status:** Accepted (2026-08-18)

## Context

Phase 6 needs an operator console for the gateway: tool registry, agent identities,
permissions, audit history, pending approvals, kill switches, and usage statistics.
It talks to the existing HTTP API and must run locally through `docker compose up`.

## Decision

A single-page **Angular 20** application (standalone components, signals) under
`src/McpGateway.AdminConsole/`, styled with **Angular Material** (Material 3). Charts
use **chart.js** via the **ng2-charts** wrapper — one small, maintained dependency
rather than hand-rolled SVG.

- **App location** sits under `src/` next to the .NET projects so every deployable
  lives in one place, even though it is an npm workspace rather than an MSBuild project.
- **Node pin:** Angular 21's CLI requires Node ≥ 22.22; the build machine runs 22.12,
  so the workspace targets Angular 20, whose toolchain supports Node 22.12. The
  container build pins Node 22 explicitly.
- **Auth model:** the operator logs in with a `client_id`/`client_secret`, which the
  console exchanges at `/oauth/token` for a bearer token. The token lives **in memory
  only** (a signal), never `localStorage`; the secret is never persisted. There is no
  refresh flow — tokens are short-lived (15 min) and the secret is not retained by
  design, so expiry means re-login. A future console-scoped longer lifetime or refresh
  token is possible but out of scope.
- **Same-origin in the container:** nginx serves the built app and reverse-proxies
  `/api` and `/oauth` to the API, so the browser makes same-origin calls. `ng serve`
  uses an equivalent dev proxy (`proxy.conf.json`). The API's CORS policy (ADR-less,
  see Program.cs) is a fallback for direct cross-origin dev use.
- **Typed models by hand:** TypeScript interfaces mirror the C# DTOs, with string-literal
  unions for the string-serialized enums. An OpenAPI document is served for reference,
  but no client-code-generation pipeline is introduced — the surface is small and
  hand-modeling keeps the toolchain simple.
- **No global state library:** component and service signals are sufficient; NgRx would
  be speculative for a CRUD console of this size.

## Consequences

- The console is a pure API client with no privileged backdoor: it is subject to the
  same `gateway.admin` scope checks (ADR 0008) as any other caller, and it hides
  operator-only actions when the logged-in token lacks the scope.
- Losing the in-memory token on refresh or after 15 minutes returns the operator to the
  login screen. This is a deliberate security trade-off, surfaced in the UI with a
  session countdown rather than hidden.
- Pinning to Angular 20 is a machine constraint, not a preference; bumping Node later
  unblocks Angular 21+ with no application changes expected.
