# ADR 028-011: Contract Package Boundary Decision

Date: 2026-08-06  
Status: Accepted - extraction deferred

## Context

Phase 028 requires transport-neutral request, response, and pagination
contracts to move into a separate package only when a concrete second consumer
or external-host contract justifies the dependency boundary. The repository
currently has one demo API host and one web client.

## Evidence

| Contract area | Current owner | .NET consumers | Boundary findings |
| --- | --- | --- | --- |
| Core requests, queries, and responses | `ShortenLink.Core.Contracts` | Application, Hosting, Infrastructure tests, API tests | Already belongs to the packable `ShortenLink.Core` library, but several records reference domain entities, security types, or service defaults. A second contracts package would duplicate that boundary rather than reduce coupling. |
| Application response envelopes and mappers | `ShortenLink.Application.Contracts.Responses` | API/Hosting composition and API tests | Mappers depend on Core domain/security entities and `ShortenLink.Auditing`; they are use-case responses, not transport-only records. The web client consumes JSON, not the .NET assembly. |
| Endpoint binding and serialization | Hosting/API endpoint mappings | One API host plus the web client | ASP.NET binding, authorization, and route concerns must remain host-owned; moving them would import ASP.NET or create a second mapping layer. |

The current dependency graph is acyclic: Core has no Application,
Infrastructure, Hosting, or ASP.NET reference; Application references Core and
Auditing; Hosting composes Application, Infrastructure, and ASP.NET; the API
composes Hosting. No independent external host currently consumes the contract
assemblies.

## Decision

Do not create a new contracts package in `028_011`. Keep the existing ownership:

- `ShortenLink.Core.Contracts` remains part of the reusable Core package for
  domain/service contracts that already have Core dependencies.
- `ShortenLink.Application.Contracts` remains the owner of use-case response
  envelopes and domain-to-response mapping.
- Hosting/API retain ASP.NET endpoint binding and route-specific request models.

This is a refactor-only decision. No JSON shape, route, public type, or
serialization behavior changes.

## Concrete extraction criteria

Reopen extraction when all of the following are true:

1. A second independent .NET host/package or an external-host contract needs
   the same records without referencing Application, Infrastructure, Hosting,
   EF Core, or ASP.NET.
2. The selected records can be separated from domain entities, validators,
   handlers, and authorization state without changing JSON names, nullability,
   or pagination semantics.
3. Serialization characterization tests cover the existing API payloads and a
   dependency/build check proves the new package is acyclic and independently
   packable.

Until those conditions exist, a new package would be speculative and would
increase versioning and mapping surface without a real reuse benefit.
