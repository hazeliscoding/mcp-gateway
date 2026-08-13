# ADR 0002 — Tool versions as owned child entities of ToolDefinition

**Status:** Accepted (2026-08-13)

## Context

A tool's identity is its snake_case name; each version carries its own contract (schemas), risk metadata, and lifecycle. Alternatives: model each (name, version) pair as an independent aggregate, or make the tool a flat row with a "current version".

## Decision

`ToolDefinition` is the aggregate root keyed by `ToolName`; versions are owned child entities keyed by (tool name, version number). Version invariants — uniqueness and strict monotonic increase — are enforced inside the aggregate. The enable/disable kill switch lives at the tool level; deprecation lives at the version level.

## Consequences

- Registering a version conflicts atomically with concurrent registrations of the same tool (single aggregate, single transaction) — no cross-aggregate uniqueness checks.
- Version monotonicity cannot be bypassed by writing versions directly; there is no repository method for versions alone.
- Value objects (`ToolName`, `ToolVersionNumber`) convert to strings in Postgres; schemas persist as `jsonb` so future phases can query them.
- Trade-off: the whole version list loads with the tool. Acceptable — registries hold tens of versions, not thousands.
