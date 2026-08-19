# Threat Model

How the gateway resists the attacks in [PLAN.md](PLAN.md) Phase 7. Each row maps an
attack to the control that stops it and the executable red-team scenario that proves it.
Attack tests live in `tests/McpGateway.IntegrationTests/Security/` and run in the normal
`dotnet test` suite.

## Trust boundaries

- **Agents are untrusted.** A caller's rights come only from its signed token; nothing
  in a request body can widen them.
- **Tool and LLM output is untrusted.** The gateway validates model output before acting
  on it and never lets the model choose its own permissions. (Output *handling* becomes
  testable once tool execution exists — see Deferred.)
- **Operators are distinguished by the `gateway.admin` scope**, enforced at the endpoint,
  not by network position.

## Covered attacks

| Attack | Control | Proven by |
|--------|---------|-----------|
| **Privilege escalation** (widen own grant) | Scopes taken only from the signed token; body fields ignored; `gateway.admin` gates management endpoints | `PrivilegeEscalationTests`, `AdminScopePolicyTests`, `AuthorizationApiTests` |
| **Token forgery / replay** | HS256 signature, issuer/audience, and expiry validated; 30s clock skew | `PrivilegeEscalationTests` (wrong-key, expired, tampered → 401) |
| **Version downgrade** | Deprecated versions denied; "latest" resolves only active versions | `VersionDowngradeTests`, `AuthorizationApiTests` |
| **Tool spoofing** | Names are validated; duplicate registration conflicts; registration is operator-only; unknown tools are not found | `ToolSpoofingTests` |
| **Cross-identity access** (ride another's grant) | Approvals are bound to the `(requester, tool, version)` triple | `GrantIsolationTests` |
| **Parameter injection** | Typed contracts; parameterized EF Core; domain value objects reject malformed input; sensitive values hashed | `ParameterInjectionTests` |
| **Secret leakage** | Secrets returned once at issue; only PBKDF2 hashes stored; audit hashes sensitive context; uniform `invalid_client` on failure | `SecretLeakageTests`, `AuditApiTests` |
| **Cross-identity audit disclosure** | Audit reads (`/api/audit`, `/api/audit/stats`) require `gateway.admin` | `AdminScopePolicyTests` |

## Deferred — no attack surface yet

The gateway authorizes tool calls but does not execute them; there is no invocation or
proxy endpoint. The following attacks target that layer and will be enforced and tested
when it is built:

- **Prompt injection in tool results** — untrusted tool output must be treated as data,
  never as instructions, and must not be able to alter subsequent authorization. The
  handling and its tests land with the execution phase.
- **Oversized responses** — response size limits and streaming caps belong to the
  execution/proxy path. (The request side is already bounded: oversized identifiers are
  rejected by validation — see `ParameterInjectionTests`.)

These are open items, not accepted risks: they are unreachable today because the feature
they target does not exist.
