---
phase: 028
title: Backend Boundary Refactor
status: complete
created_at: 2026-08-03
updated_at: 2026-08-03
current_task:
task_count: 2
done_count: 2
depends_on:
  - 027
---

# Phase 028 Summary

## Phase Goal

Refactor the backend toward one reusable hosting boundary and thinner demo API
composition without changing public route behavior or business semantics.

## Phase Done Criteria

- The demo API does not maintain duplicate endpoint mapping or backend business
  logic that already belongs to reusable projects.
- Shared hosting remains the canonical endpoint composition boundary.
- Existing API, application, infrastructure, and core verification stays green.

## Scope

In:

- Consolidating duplicated API and reusable hosting endpoint mappings.
- Keeping route names, authorization policies, request contracts, and response
  behavior compatible.
- Removing only proven-unused demo-host duplication.

Out:

- New product behavior, database schema changes, frontend refactors, and broad
  service decomposition.

## Task Index

| Task | Title | Status | Done At |
|---|---|---|---|
| 028_001 | Consolidate API endpoint mapping boundary | done | 2026-08-03 |
| 028_002 | Extract provider-neutral queue library with memory and RabbitMQ adapters | done | 2026-08-03 |

## Current Task

Phase 028 is complete. The endpoint boundary is consolidated and the queue
abstraction is provider-neutral with memory and RabbitMQ implementations.

## Task Notes

### 028_001 - Consolidate API endpoint mapping boundary

#### Step Goal

Remove the unused duplicate `ShortLinkManagementEndpoints` mapping from the
demo API so `ShortenLinkEndpointMappings` in shared hosting is the canonical
management route boundary.

#### Dependency

- Phase 027 completed the current API contracts and route surface.
- `src/ShortenLink.Api/Program.cs` already composes shared hosting mappings.

#### Scope

In:

- Delete the duplicate API management endpoint mapping file.
- Remove imports and composition assumptions that exist only for that dead
  mapping.
- Preserve all routes through shared hosting and add a route-surface guard if
  needed.

Out:

- Endpoint behavior changes, route renames, authorization changes, and moving
  feature handlers out of Application.

#### Acceptance Criteria

- `Program.cs` remains the only API composition path for short-link management.
- `src/ShortenLink.Api/Endpoints/ShortLinkManagementEndpoints.cs` is removed
  because it is not mapped by the host.
- Shared endpoint routes continue to build and pass API tests.
- No application or infrastructure behavior changes are introduced.

#### Foundation for Next Step

The backend has one verified endpoint composition boundary, making later
service and persistence refactors safer to review and test.

#### Affected Files

- `src/ShortenLink.Api/Endpoints/ShortLinkManagementEndpoints.cs`
- `src/ShortenLink.Api/Program.cs`
- `shared/ShortenLink.Hosting/ShortenLinkEndpointMappings.cs`
- `tests/ShortenLink.Api.Tests/`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`

#### Done Notes

Done. Removed the unused duplicate API management endpoint mapping and fixed a
stale Health endpoint import that the duplicate file had masked. The shared
`ShortenLinkEndpointMappings` remains the only short-link management mapping
composed by `Program.cs`; route behavior is unchanged. Verification passed with
a warning-free solution build and all 213 solution tests.

### 028_002 - Extract provider-neutral queue library with memory and RabbitMQ adapters

#### Step Goal

Move queue contracts and provider wiring out of the hosting implementation into
one reusable messaging library that supports the existing in-memory queue for
local development and an opt-in RabbitMQ provider for distributed workloads.

#### Dependency

- `028_001` establishes a single reusable hosting composition boundary.
- Existing audit and click background queue flows in `shared/ShortenLink.Hosting`.

#### Scope

In:

- Add a packable provider-neutral queue contract with publish/consume,
  cancellation, bounded in-memory backpressure, and acknowledgement outcome
  semantics.
- Provide memory and RabbitMQ implementations behind the same contract, with
  configuration selecting the provider and memory remaining the default.
- Adapt audit and click processing to the queue abstraction without changing
  event payloads, retry-safe behavior, or local developer setup.
- Add provider-neutral tests plus RabbitMQ adapter contract tests that do not
  require a live broker.

Out:

- Automatic broker provisioning, deployment manifests, hosted scheduler work,
  cross-service event schema redesign, and frontend changes.

#### Acceptance Criteria

- A reusable messaging library can be referenced without depending on the demo
  API host.
- Memory mode preserves current local behavior and bounded queue semantics.
- RabbitMQ mode uses explicit connection/queue options, acknowledgement and
  cancellation boundaries, and never logs credentials or message payload
  secrets.
- Audit and click consumers can switch providers through configuration only;
  application handlers do not reference `System.Threading.Channels` directly.
- Existing API, application, infrastructure, and core behavior remains green.

#### Foundation for Next Step

Backend asynchronous work has one provider-neutral queue boundary, allowing
later refactors to move workers or add retry/dead-letter policy without
rewriting application handlers.

#### Affected Files

- `shared/ShortenLink.Messaging/`
- `shared/ShortenLink.Hosting/`
- `src/ShortenLink.Application/`
- `src/ShortenLink.Api/appsettings.json`
- `tests/ShortenLink.Application.Tests/`
- `tests/ShortenLink.Messaging.Tests/`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Focused memory backpressure, acknowledgement/requeue, RabbitMQ adapter
  construction, cancellation, and provider-selection tests.

#### Done Notes

Done. Added the packable `ShortenLink.Messaging` library with a provider-neutral
`IMessageQueue<T>` contract, bounded memory queue, and durable RabbitMQ adapter.
Migrated audit and click workers out of direct channel usage, added queue
configuration and validation, documented both providers in the package README,
and added six provider/acknowledgement/backpressure/cancellation tests. The
RabbitMQ adapter now uses separate publisher/consumer channels, publisher
confirmations, bounded prefetch, and explicit consumer cancellation. Full
solution build, 219 tests, and package creation passed.

## Next Task Proposal

After 028_002, identify the next smallest backend seam to refactor without
mixing queue provider concerns into application business logic.
