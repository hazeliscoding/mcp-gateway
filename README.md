# MCP Gateway 🛡️

An enterprise gateway that safely exposes internal capabilities to AI agents through MCP. Agents never talk to internal services directly — every tool call passes through authentication, policy-based authorization, schema validation, risk classification, and audit logging.

## Why

Giving an agent raw access to infrastructure is a non-starter in any real organization. This gateway treats tools as governed infrastructure: registered, versioned, permissioned, and auditable — with human approval gates for privileged actions and kill switches for everything.

## Architecture

```text
Agent → MCP Gateway → [ AuthN → AuthZ → Policy → Validation → Audit ] → Internal Services
```

Risk classes drive policy: `ReadOnly` runs automatically, `Privileged` requires approval, `Destructive` is prohibited or multi-party.

## Stack

.NET 10 · ASP.NET Core · PostgreSQL · EF Core · Angular · OpenTelemetry · Docker Compose · xUnit + Testcontainers

## Status 🚧

In progress — see [docs/PLAN.md](docs/PLAN.md) for the phased build plan and [docs/adr/](docs/adr/) for decisions.

- [x] Phase 1 — Tool registry (registration, discovery, versioning, kill switch, deprecation)
- [x] Phase 2 — Authentication (user/agent/service identities, OAuth2 client credentials, JWT bearer)
- [x] Phase 3 — Authorization (deterministic ABAC engine: kill switch, version lifecycle, scope coverage)
- [ ] Phase 4 — Risk classification
- [ ] Phase 5 — Audit trail
- [ ] Phase 6 — Angular admin console
- [ ] Phase 7 — Attack testing

## Running Locally

```bash
docker compose up --build
```

The API starts on `http://localhost:8080`, applies migrations automatically, and seeds a bootstrap admin identity (`gateway_admin` / `local-dev-bootstrap-secret` — local dev only). All APIs require a token. Get one, then register and discover a tool:

```bash
TOKEN=$(curl -s -X POST http://localhost:8080/oauth/token \
  -d "grant_type=client_credentials&client_id=gateway_admin&client_secret=local-dev-bootstrap-secret" \
  | jq -r .access_token)

curl -X POST http://localhost:8080/api/tools \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "get_queue_metrics",
    "version": "1.0",
    "description": "Returns queue depth and consumer lag.",
    "riskLevel": "ReadOnly",
    "approvalRequired": false,
    "requiredScopes": ["queue.read"],
    "timeoutSeconds": 10,
    "inputSchema": {"type": "object"},
    "outputSchema": {"type": "object"}
  }'

curl -H "Authorization: Bearer $TOKEN" http://localhost:8080/api/tools
```

Identities are managed at `/api/identities`; client secrets are generated server-side, returned exactly once, and stored only as PBKDF2 hashes.

### Authorization

Before an action runs, ask the gateway whether it is allowed. A deterministic policy engine evaluates the kill switch, version lifecycle, and scope coverage, and returns a decision with machine-readable reasons. The caller's scopes come from its token — never from the request body — so a caller cannot widen its own grant:

```bash
curl -X POST http://localhost:8080/api/tools/redrive_dead_letter_queue/authorize \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{ "action": "Invoke" }'
```

The response is HTTP 200 for both permit and deny — evaluating is what succeeded:

```json
{
  "permit": false,
  "toolName": "redrive_dead_letter_queue",
  "version": "1.0.0",
  "action": "Invoke",
  "reasons": [{ "code": "MissingScopes", "message": "Missing required scope(s): queue.redrive." }]
}
```

Omit `version` to target the latest active version. This decision endpoint is the gate that tool execution will call through in later phases.

Tests (integration tests spin up Postgres via Testcontainers — Docker required):

```bash
dotnet test
```
