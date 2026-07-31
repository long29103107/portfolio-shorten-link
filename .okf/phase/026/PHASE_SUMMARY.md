---
phase: 026
title: Library Extensibility And Observability
status: complete
created_at: 2026-07-31
updated_at: 2026-07-31
current_task: null
task_count: 3
done_count: 3
depends_on:
  - 025
---

# Phase 026 Summary

## Phase Goal

Expose stable, safe extension points for code generation, lifecycle events,
metrics, and custom analytics without leaking secrets or HTTP concerns into
Core.

## Phase Done Criteria

- Consumers can replace short-code generation and collision handling through
  documented DI/configuration contracts.
- Lifecycle and redirect events have versioned, cancellation-aware payloads
  with secret-exclusion guarantees.
- Health checks, activities/meters, and diagnostic logging guidance are opt-in
  and stable for external hosts.
- Extension and observability contracts have focused tests and do not slow a
  successful redirect by default.

## Scope

In:

- Configurable short-code generation and collision handling.
- Lifecycle/redirect event contracts and configurable sinks.
- Health checks, OpenTelemetry activities/meters, and diagnostic logging names.
- Contract tests for extension payload safety and cancellation behavior.

Out:

- Idempotency, bulk import/export, tenant partitioning, and retention workers;
  those belong to Phase 027.

## Task Index

| Task | Title | Status | Done At |
|---|---|---|---|
| 026_001 | Configurable random code generation and atomic collision retry | done | 2026-07-31 |
| 026_002 | Versioned lifecycle and redirect event contracts | done | 2026-07-31 |
| 026_003 | Health checks, activities, meters, and diagnostic logging guidance | done | 2026-07-31 |

## Current Task

No task is active. Phase 026 is complete.

## Task Notes

### 026_003 - Health checks, activities, meters, and diagnostic logging guidance

#### Step Goal

Expose opt-in health checks, ActivitySource/meter contracts, and structured
diagnostic logging guidance for database, cache, analytics, and configuration
without adding redirect work when observability is disabled.

#### Dependency

- `026_001` provider-neutral persistence and collision boundaries.
- `026_002` safe lifecycle and redirect event boundary.

#### Scope

In:

- Stable diagnostic names and redirect activity/counter instrumentation.
- Opt-in configuration for instrumentation and built-in health checks.
- Database, cache, analytics, and configuration health-check seams.
- Structured, secret-free mediator diagnostic logging guidance and tests.

Out:

- OpenTelemetry exporter packages, dashboards, and hosted telemetry agents.
- Changes to redirect results, event payloads, or persistence semantics.

#### Acceptance Criteria

- Consumers can opt into health checks and diagnostics through configuration or
  the public hosting extension.
- Activity and meter names are stable, low-cardinality, and never include URLs,
  credentials, tokens, request metadata, or destination data.
- Database, cache, analytics, and configuration health checks report safe,
  actionable status without exposing connection strings or secrets.
- Default registration remains a no-op and successful redirects keep their
  existing behavior and timing path when observability is disabled.

#### Foundation for Next Step

External hosts can attach OpenTelemetry listeners, map health endpoints, and
route structured diagnostics without referencing the demo API or changing the
short-link business contract.

#### Affected Files

- `src/ShortenLink.Core/Diagnostics/`
- `src/ShortenLink.Application/Services/ShortLinkService.cs`
- `src/ShortenLink.Application/Behaviors/LoggingPipelineBehavior.cs`
- `shared/ShortenLink.Hosting/ShortenLinkOptions.cs`
- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `shared/ShortenLink.Hosting/ShortenLinkHealthChecks.cs`
- `README.md`
- `tests/ShortenLink.Core.Tests/`
- `tests/ShortenLink.Api.Tests/`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Focused contract tests for opt-in registration, health status, and safe
  activity/meter names.

#### Done Notes

Done. Added opt-in `ShortenLink` ActivitySource/meter contracts, redirect
outcome instrumentation, idempotent built-in health-check registration, safe
database/cache/analytics/configuration checks, and secret-free diagnostic event
guidance. Default hosts do not register health checks or emit redirect
diagnostics unless configured. Verification passed with a solution build and
182 tests.

## Completed Notes

- `026_001` added validated `ShortenLink:Code:DefaultLength` and
  `ShortenLink:Code:MaxRetry` options while preserving random Base62 defaults.
- `026_001` added `ShortLinkCodeConflictException`, EF SQLite/PostgreSQL
  duplicate-code translation, atomic retry behavior, and change-tracker cleanup
  after a failed insert.
- Verification passed: solution build and 176 tests. PostgreSQL live conflict
  execution remains an environment-only gap because no PostgreSQL server is
  available; existing provider model checks passed.
- `026_002` added versioned, secret-free lifecycle and redirect event payloads,
  a non-blocking opt-in sink contract, fail-open publishing, and coverage for
  create/update/status/delete/redirect sequences.
- Verification passed: solution build and 178 tests.
- `026_003` added opt-in redirect ActivitySource/meter contracts, safe
  configuration/database/cache/analytics health checks, and stable secret-free
  diagnostic event guidance. Default registration remains a no-op.
- Verification passed: solution build and 182 tests.

## Next Task Proposal

Phase 027 / `027_001` - Idempotent create request contract and persistence key
boundary.

- Add an optional idempotency key contract for create operations.
- Preserve the simple local path when no key is supplied.
- Keep duplicate/idempotent replay behavior provider-neutral before bulk import
  work begins.

### 026_001 - Configurable random code generation and atomic collision retry

#### Step Goal

Make generated short-code length and retry limits configurable while ensuring
concurrent creates retry only on a duplicate-code conflict and preserve all
other persistence failures.

#### Dependency

- Phase 025 provider-neutral repository and transaction contracts.
- The existing random Base62 `IShortCodeGenerator` and unique code index.

#### Scope

In:

- Wire `ShortenLink:Code:DefaultLength` and `ShortenLink:Code:MaxRetry` through
  Hosting configuration and validation.
- Define the shared duplicate-code conflict signal used by built-in and custom
  stores.
- Retry create on duplicate-code conflicts up to the configured limit.
- Add focused core, provider, and configuration contract coverage.

Out:

- Timestamp, Snowflake, UUIDv7, or other alternate generator implementations.
- Lifecycle events, metrics, health checks, and OpenTelemetry integration.

#### Relevant Standards

- `.okf/standards/architecture.md`
- `.okf/standards/coding-style.md`
- `.okf/standards/api-design.md`
- `.okf/standards/testing.md`
- `PRODUCT_VISION.md`

#### Affected Files

- `shared/ShortenLink.Hosting/ShortenLinkOptions.cs`
- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `src/ShortenLink.Application/Services/ShortLinkService.cs`
- `src/ShortenLink.Core/Abstractions/`
- `src/ShortenLink.Infrastructure/Repositories/`
- `tests/ShortenLink.Core.Tests/`
- `tests/ShortenLink.Infrastructure.Tests/`
- `tests/ShortenLink.Api.Tests/`

#### Acceptance Criteria

- A consumer can configure code length and retry limit without changing code.
- Defaults remain random Base62 with length 7 and a positive retry limit.
- Concurrent duplicate candidates do not create duplicate codes or leak a
  provider-specific exception through the public Application contract.
- Non-duplicate persistence failures are not swallowed or retried as collisions.
- Built-in SQLite/PostgreSQL and in-memory contract tests pass.

#### Foundation for Next Step

The create/resolve path has a stable, provider-neutral success and collision
boundary that lifecycle and redirect event sinks can observe without coupling to
EF Core or a particular generator.

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Provider contract tests covering duplicate and non-duplicate persistence
  failures.

#### Done Notes

Done. Added validated code-generation options, atomic duplicate-code retry,
provider-specific EF conflict translation, and focused in-memory/SQLite tests.
Verification passed with a solution build and 176 tests. PostgreSQL live
conflict execution was not run because no PostgreSQL server is available in
this environment; the existing PostgreSQL model/provider checks passed.

### 026_002 - Versioned lifecycle and redirect event contracts

#### Step Goal

Expose safe, versioned lifecycle and redirect events through a non-blocking,
opt-in sink without adding event-delivery failures or destination/identity
data to the business operation contract.

#### Dependency

- `026_001` provider-neutral create and redirect boundaries.
- Existing `ShortLinkService` create, update, lifecycle, delete, and resolve
  operations.

#### Scope

In:

- Versioned event type constants and a secret-free event payload.
- `IShortLinkEventSink` with cancellation-aware, non-blocking `TryPublish`.
- Event publication for create, update, activate, deactivate, delete, and
  successful redirect operations.
- Fail-open sink behavior and focused contract tests/documentation.

Out:

- Background event queue implementations, OpenTelemetry exporters, and
  health checks; those are proposed for `026_003`.

#### Relevant Standards

- `.okf/standards/architecture.md`
- `.okf/standards/coding-style.md`
- `.okf/standards/api-design.md`
- `.okf/standards/testing.md`
- `PRODUCT_VISION.md`

#### Affected Files

- `src/ShortenLink.Core/Events/`
- `src/ShortenLink.Core/Abstractions/IShortLinkEventSink.cs`
- `src/ShortenLink.Application/Services/ShortLinkService.cs`
- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `tests/ShortenLink.Core.Tests/ShortLinkServiceTests.cs`
- `README.md`

#### Acceptance Criteria

- Event payloads have an explicit version and stable event type names.
- Payloads exclude destination URLs, identities, credentials, tokens, hashes,
  and request metadata by construction.
- Create, update, activate, deactivate, delete, and successful redirect events
  are published when an opt-in sink is registered.
- Sink failures do not fail or block successful short-link operations.
- No sink registration preserves existing no-op behavior.

#### Foundation for Next Step

The application now exposes a safe event boundary that health/telemetry
instrumentation and external queues can consume without coupling Core to HTTP,
EF Core, or secrets.

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Lifecycle sequence, secret-exclusion, and sink-failure tests.

#### Done Notes

Done. Added versioned event contracts, safe payload construction, opt-in DI
sink resolution, fail-open event publication, README guidance, and 2 focused
tests. Verification passed with a solution build and 178 tests.

## Scan Rule

Keep all extensibility and observability work inside Phase 026 until its done
criteria are verified. Do not start Phase 027 early.
