# PROJECT 4 — Secure MCP Gateway and Tool Registry

## Goal

Build an enterprise gateway that safely exposes internal capabilities to AI agents through MCP.

The gateway should be treated as infrastructure rather than an example chatbot.

## Architecture

```text
Agent
  |
  v
MCP Gateway
  |
  +--> Authentication
  |
  +--> Authorization
  |
  +--> Policy Engine
  |
  +--> Schema Validation
  |
  +--> Audit Logging
  |
  +--> Tool Registry
  |
  +--> Approval Engine
  |
  v
Internal Services
```

## Tool Metadata

Define metadata such as:

```json
{
  "name": "redrive_dead_letter_queue",
  "version": "1.2",
  "riskLevel": "privileged",
  "approvalRequired": true,
  "requiredScopes": [
    "queue.read",
    "queue.redrive"
  ],
  "timeoutSeconds": 30
}
```

## Phase 1 — Tool Registry

Support:

- Registration.
- Discovery.
- Versioning.
- Enable/disable.
- Deprecation.

## Phase 2 — Authentication

Support:

- User identity.
- Agent identity.
- OAuth.
- Service identities.

Never give the model access to raw credentials.

## Phase 3 — Authorization

Use policy-based authorization.

Example:

```text
User
Agent
Tool
Environment
Resource
Action
```

Evaluate all before execution.

## Phase 4 — Risk Classification

Classes:

```text
ReadOnly
Write
Privileged
Destructive
```

Policy example:

```text
ReadOnly -> automatic
Write -> automatic depending on scope
Privileged -> approval
Destructive -> prohibited or multi-party approval
```

## Phase 5 — Audit Trail

Record:

```text
Who
Which agent
Which tool
Input hash
Output hash
Timestamp
Trace ID
Approval
Result
```

Redact sensitive data.

## Phase 6 — Angular Admin Console

Pages:

- Tool registry.
- Agent identities.
- Permissions.
- Audit history.
- Pending approvals.
- Kill switches.
- Usage statistics.

## Phase 7 — Attack Testing

Test:

- Prompt injection in tool results.
- Privilege escalation.
- Tool spoofing.
- Version downgrade.
- Parameter injection.
- Excessively large responses.
- Secret leakage.
- Cross-tenant access.

## Portfolio Demo

Demonstrate:

1. Agent discovers available tools.
2. Read-only tool executes.
3. Agent attempts privileged action.
4. Gateway blocks execution.
5. Human approves.
6. Tool executes.
7. Full audit history is visible.

