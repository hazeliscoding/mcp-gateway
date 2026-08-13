# ADR 0001 — Layered solution with plain application services

**Status:** Accepted (2026-08-13)

## Context

The gateway needs clear boundaries between domain rules, orchestration, persistence, and HTTP. A common .NET reflex is to add MediatR (or a CQRS framework) from day one.

## Decision

Four projects — Domain → Application → Infrastructure → WebApi — with the application layer exposing **plain service classes** returning typed `OperationResult<T>` values. No MediatR, no pipeline behaviors, no in-process message bus.

## Consequences

- One fewer dependency and no indirection between an endpoint and the code it calls; handlers are discoverable by "go to definition".
- Cross-cutting behaviors (auth, audit) arrive in later phases as middleware or decorators; if a real pipeline need emerges, MediatR can be introduced then with evidence.
- Domain exceptions never cross the API boundary: services convert them to `Validation | NotFound | Conflict` outcomes, endpoints only map outcomes to status codes.
