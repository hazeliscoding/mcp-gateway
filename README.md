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
- [ ] Phase 2 — Authentication
- [ ] Phase 3 — Authorization
- [ ] Phase 4 — Risk classification
- [ ] Phase 5 — Audit trail
- [ ] Phase 6 — Angular admin console
- [ ] Phase 7 — Attack testing

## Running Locally

```bash
docker compose up --build
```

The API starts on `http://localhost:8080` and applies migrations automatically. Register and discover a tool:

```bash
curl -X POST http://localhost:8080/api/tools \
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

curl http://localhost:8080/api/tools
```

Tests (integration tests spin up Postgres via Testcontainers — Docker required):

```bash
dotnet test
```
