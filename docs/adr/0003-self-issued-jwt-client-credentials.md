# ADR 0003 — Self-issued JWTs via OAuth2 client credentials

**Status:** Accepted (2026-08-13)

## Context

Phase 2 needs authenticated users, agents, and service identities. The realistic alternatives were an external IdP (e.g. Keycloak in compose) or the gateway issuing its own tokens.

## Decision

The gateway is its own token authority: identities live in Postgres (secrets stored as PBKDF2-SHA256 hashes, shown to the caller exactly once), `/oauth/token` implements the client-credentials grant, and APIs validate standard HS256 JWTs. Unknown client, wrong secret, and disabled identity fail identically, with a decoy hash verification to keep timing uniform.

## Consequences

- Zero extra containers; integration tests stay fast and fully deterministic.
- Validation uses the standard ASP.NET JWT bearer middleware, so swapping to an external IdP later is a configuration change (authority/audience) plus removing the token endpoint — API code is untouched.
- Human users currently authenticate with client credentials too; a proper interactive OIDC flow only makes sense once the Angular console exists (Phase 6) and will be revisited then.
- HS256 with a shared config key is acceptable for a single-service local deployment; moving to asymmetric keys (or an IdP) is the documented path if a second token consumer ever appears.
- Credential isolation: raw secrets exist only in the registration/rotation HTTP response; they are never stored, logged, or embedded in anything a model could read.
