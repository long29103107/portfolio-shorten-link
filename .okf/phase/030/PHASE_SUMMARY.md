---
phase: 030
title: Backend Optimization Follow-up
status: active
created_at: 2026-08-03
updated_at: 2026-08-03
current_task: 030_002
task_count: 8
done_count: 1
depends_on:
  - 029
---

# Phase 030 Summary

## Phase Goal

Continue backend optimization after the query/index pass by improving hot-path
performance, making conventions consistent, and separating infrastructure
boundaries from business code without changing public API behavior.

## Phase Done Criteria

- Hot read paths use seek-friendly pagination, bounded materialization, and
  cache/queue protection appropriate to their workload.
- Database, cache, and background-queue behavior has actionable latency,
  failure, and backpressure telemetry.
- Large composition and service files are split into cohesive modules while
  preserving dependency direction and public contracts.
- Options, logging, schema evolution, tenant isolation, and session/security
  responsibilities follow one documented convention.
- Durable schema and event-delivery boundaries are explicit where data loss or
  cross-tenant access would otherwise be possible.
- Focused tests and the full solution verification stay green after every task.

## Task Index

| Task | Title | Category | Status | Done At |
|---|---|---|---|---|
| 030_001 | Replace offset hot paths and protect cache misses | Performance | done | 2026-08-03 |
| 030_002 | Normalize UTC timestamps and project lean read models | Performance | planned | |
| 030_003 | Add observability, backpressure, and bounded retry metrics | Performance | planned | |
| 030_004 | Split large services and composition modules | Convention | planned | |
| 030_005 | Unify options binding and structured logging conventions | Convention | planned | |
| 030_006 | Introduce versioned schema migration and compatibility boundaries | Architecture | planned | |
| 030_007 | Add tenant-aware audit and durable event delivery boundaries | Architecture | planned | |
| 030_008 | Split hosting/package boundaries and session responsibilities | Architecture | planned | |

## Current Task

`030_001` is complete. `030_002` is the next planned task and remains
unstarted until explicitly selected.

## Task Notes

### 030_001 - Replace offset hot paths and protect cache misses

#### Step Goal

Make high-volume list traversal seek-friendly and prevent concurrent cache
misses for the same short code from stampeding the database.

#### Use Cases

- An export or admin table walks millions of links without increasing `Skip`
  cost on every page.
- A popular short link expires from cache and many requests arrive together;
  only one request refreshes the value while others share the result.
- A missing or invalid code is negatively cached briefly instead of repeatedly
  querying storage.

#### Scope

In: cursor/keyset pagination for remaining offset hot paths, compatible page
fallback where required, single-flight cache refresh, bounded negative caching,
and regression tests.

Out: public route removal, cache-provider replacement, and schema redesign.

#### Acceptance Criteria

- New traversal paths do not use unbounded offset scans for large datasets.
- Existing page contracts remain compatible or expose an explicit cursor path.
- Concurrent misses for one key produce at most one storage refresh.
- Negative-cache behavior has a bounded TTL and does not cache transient errors.
- Focused repository/cache tests and the full solution suite pass.

#### Foundation for Next Step

The hot paths have predictable traversal and cache-load behavior, allowing the
next task to normalize timestamp storage and reduce read-model materialization.

#### Affected Files

- `src/ShortenLink.Infrastructure/Repositories/EfCoreShortLinkRepository.cs`
- `shared/ShortenLink.Hosting/DistributedShortLinkCache.cs`
- Related application, API, and infrastructure tests.

#### Verification

- Focused pagination and cache-concurrency tests.
- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- Full solution tests with the repository's Windows EventLog override when needed.

#### Done Notes

Done. Added an explicit created-at cursor path for filtered, descending-created
traversal while retaining offset pagination as the compatibility fallback for
other sort/filter combinations. Added a singleton cache loader capability with
single-flight miss coalescing, short-lived negative entries, cancellation-safe
waits, and cache invalidation cleanup. Configured and validated
`NegativeEntryTtlSeconds` with a default of 10 seconds. Focused cursor and
cache-concurrency tests passed, followed by the full solution build and test
suite: 223/223 tests passed.

### 030_002 - Normalize UTC timestamps and project lean read models

#### Step Goal

Store/query timestamps in one provider-friendly UTC representation and project
only fields needed by list and analytics responses.

#### Use Cases

- SQLite and PostgreSQL use the same sargable range/order semantics without
  scattered raw SQL conversion workarounds.
- A list of 50 links does not hydrate full entities and navigation state when
  the response needs only summary fields.
- Composite timestamp indexes are used for expiration, cursor, and audit reads.

#### Scope

In: persistence timestamp representation, provider dialect isolation, DTO/read
model projections, migration/tests, and compatibility handling for existing
data.

Out: unrelated domain model redesign and API response shape changes.

#### Acceptance Criteria

- Hot timestamp predicates and ordering remain index-friendly on supported
  providers.
- Read queries select only required columns before materialization.
- Existing timestamps and response semantics remain compatible.
- Provider-specific SQL is isolated behind one persistence boundary.

#### Foundation for Next Step

Stable query shapes and lean materialization make latency and queue metrics
meaningful in the observability task.

#### Affected Files

- `src/ShortenLink.Infrastructure/Persistence/ShortLinkDbContext.cs`
- `src/ShortenLink.Infrastructure/Persistence/ShortLinkDatabaseSchema.cs`
- `src/ShortenLink.Infrastructure/Repositories/`
- Related contracts and tests.

#### Verification

- Provider-specific repository/schema tests.
- Migration or compatibility rehearsal against SQLite and PostgreSQL.
- Full solution build and tests.

#### Done Notes

Planned; not started.

### 030_003 - Add observability, backpressure, and bounded retry metrics

#### Step Goal

Expose actionable metrics and structured events for database/cache/queue
latency, drops, retries, queue depth, and worker throughput.

#### Use Cases

- Operators can distinguish a slow database from a cache miss storm.
- Queue saturation is visible before request latency becomes an outage.
- Rejected or dead-lettered messages include enough context to diagnose the
  failure without logging secrets or full payloads.

#### Scope

In: `Activity`/`Meter` instrumentation, queue depth/backpressure hooks,
bounded retry outcome metrics, dashboard-friendly names, and tests.

Out: selecting a hosted monitoring vendor and changing business retry policy
without evidence.

#### Acceptance Criteria

- Key repository, cache, and worker paths emit duration/count/failure signals.
- Queue saturation and rejected-message outcomes are observable.
- Correlation identifiers are preserved without sensitive data leakage.
- Instrumentation is disabled or low-overhead when no listener is configured.

#### Foundation for Next Step

Measured boundaries provide baselines for safely splitting large modules in the
convention tasks.

#### Affected Files

- `shared/ShortenLink.Hosting/`
- `src/ShortenLink.Infrastructure/`
- `shared/ShortenLink.Messaging/` or queue library projects
- Tests and observability documentation.

#### Verification

- Focused instrumentation tests with an in-memory listener.
- Queue saturation/failure tests.
- Full solution build and tests.

#### Done Notes

Planned; not started.

### 030_004 - Split large services and composition modules

#### Step Goal

Split oversized service, DI, endpoint, DbContext, and response-contract files
into cohesive modules while retaining the same public behavior.

#### Use Cases

- A change to link creation does not require navigating an unrelated 25 KB
  service file.
- Hosting registration can be reviewed by feature/provider without accidental
  order changes.
- Endpoint and EF model changes have focused ownership and tests.

#### Scope

In: `ShortLinkService`, hosting registrations, endpoint mappings, DbContext
configurations, and response contract grouping.

Out: business rule changes and public route/DTO changes.

#### Acceptance Criteria

- Each extracted module has one clear responsibility and stable DI lifetime.
- No circular dependency or duplicate registration is introduced.
- Public routes, DTOs, and domain behavior remain unchanged.
- File-size and namespace conventions are documented or enforced where useful.

#### Foundation for Next Step

The codebase has smaller seams for applying unified options and logging rules.

#### Affected Files

- `src/ShortenLink.Application/Services/ShortLinkService.cs`
- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `shared/ShortenLink.Hosting/ShortenLinkEndpointMappings.cs`
- `src/ShortenLink.Infrastructure/Persistence/ShortLinkDbContext.cs`
- `src/ShortenLink.Application/Contracts/Responses/ApplicationResponses.cs`

#### Verification

- Architecture/build checks and focused endpoint/service tests.
- Full solution build and tests.

#### Done Notes

Planned; not started.

### 030_005 - Unify options binding and structured logging conventions

#### Step Goal

Use one validated options source per feature and replace ad-hoc diagnostic
writes with structured, configurable logging.

#### Use Cases

- Worker activation and request behavior read the same effective configuration
  snapshot instead of separately bound values.
- Operators can filter logs by feature, short code, correlation id, and outcome.
- Tests can replace options/logging without depending on process-global state.

#### Scope

In: options registration/validation, `IOptions` usage, logging pipeline,
event IDs, sensitive-data redaction, and convention tests/docs.

Out: changing log retention or adopting a vendor-specific sink.

#### Acceptance Criteria

- Each feature has one canonical options binding path with startup validation.
- `Trace.WriteLine`-style production diagnostics are removed from hot paths.
- Logs use stable event names/IDs and avoid tokens, payloads, and secrets.
- Existing configuration keys remain compatible or have an explicit migration.

#### Foundation for Next Step

Configuration and diagnostics are consistent before schema and package
boundaries are changed.

#### Affected Files

- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `src/ShortenLink.Application/Behaviors/LoggingPipelineBehavior.cs`
- Feature option records and related tests.

#### Verification

- Options validation and logging-capture tests.
- Full solution build and tests.

#### Done Notes

Planned; not started.

### 030_006 - Introduce versioned schema migration and compatibility boundaries

#### Step Goal

Replace implicit schema evolution and scattered provider SQL with a versioned,
repeatable migration boundary that includes all current indexes.

#### Use Cases

- A new deployment upgrades an existing database deterministically instead of
  relying on `EnsureCreated` or startup side effects.
- Fresh SQLite and PostgreSQL databases receive the same required indexes.
- A failed migration can be diagnosed and resumed safely.

#### Scope

In: migration/version metadata, provider-specific compatibility adapters,
legacy schema rehearsal, and composite-index upgrade scripts.

Out: changing business tables beyond the required migration boundary.

#### Acceptance Criteria

- Fresh and existing databases converge to the same schema version.
- Current composite indexes are present in both fresh and upgrade paths.
- Migration execution is idempotent/transaction-safe for supported providers.
- Startup failures identify the schema version and actionable remediation.

#### Foundation for Next Step

Schema changes have a durable foundation for tenant audit columns and outbox
records.

#### Affected Files

- `src/ShortenLink.Infrastructure/Persistence/ShortLinkDatabaseSchema.cs`
- `src/ShortenLink.Infrastructure/Persistence/ShortLinkDbContext.cs`
- Migration/compatibility tests and deployment documentation.

#### Verification

- Fresh database and upgrade rehearsal for each supported provider.
- Schema/index assertions and full solution tests.

#### Done Notes

Planned; not started.

### 030_007 - Add tenant-aware audit and durable event delivery boundaries

#### Step Goal

Make audit ownership tenant-explicit and provide a durable outbox boundary for
events whose loss or cross-tenant visibility is unacceptable.

#### Use Cases

- A tenant audit query cannot accidentally discover another tenant's event by
  sharing an owner or short code.
- A request that commits a link change and emits an audit/click event either
  persists both or leaves a retryable outbox record.
- Poison messages are isolated through explicit retry/dead-letter state.

#### Scope

In: tenant fields/constraints/indexes for audit, outbox record/dispatcher
contracts, idempotency keys, and queue integration tests.

Out: replacing RabbitMQ or introducing a distributed workflow engine.

#### Acceptance Criteria

- Audit writes and reads require explicit tenant context.
- Outbox records are transactionally coupled to the business write and safely
  retried/marked terminal.
- Dispatch is idempotent and observable.
- Existing audit and analytics behavior remains compatible for current data.

#### Foundation for Next Step

Durable event and tenant boundaries allow hosting/package extraction without
leaking persistence concerns into business code.

#### Affected Files

- Audit domain contracts/entities/repositories.
- `src/ShortenLink.Infrastructure/Persistence/`
- Queue abstractions/adapters and integration tests.

#### Verification

- Tenant isolation, transaction, idempotency, and retry tests.
- Provider migration rehearsal and full solution tests.

#### Done Notes

Planned; not started.

### 030_008 - Split hosting/package boundaries and session responsibilities

#### Step Goal

Separate reusable ASP.NET Core integration, workers, and security-session
responsibilities so business projects do not depend on host composition details.

#### Use Cases

- A consumer can reuse endpoint/auth middleware registration without importing
  the entire demo host and EF implementation.
- Session token crypto can be tested independently from permission resolution
  and user-session orchestration.
- Worker hosting can be replaced without changing application use cases.

#### Scope

In: hosting package boundaries, worker registration adapters, session token
service extraction, permission resolver extraction, and dependency direction.

Out: changing authentication algorithms or public API contracts.

#### Acceptance Criteria

- Reusable host integration has no unnecessary dependency on the demo API.
- Session token, permission, and orchestration responsibilities are separate
  interfaces with explicit lifetimes.
- Dependency graph remains acyclic and business projects stay host-agnostic.
- Existing authentication, authorization, and API tests remain green.

#### Foundation for Next Step

The backend has clear package seams for future provider swaps and independent
deployment/testing.

#### Affected Files

- `shared/ShortenLink.Hosting/ShortenLink.Hosting.csproj`
- `shared/ShortenLink.Hosting/SecuritySessionServiceAdapter.cs`
- `shared/ShortenLink.Hosting/ShortenLinkUserSessionService.cs`
- `shared/ShortenLink.Hosting/ShortenLinkAuthorizationService.cs`
- New host/security package projects and related tests.

#### Verification

- Dependency/build graph checks.
- Focused authentication/authorization tests.
- Full solution build and tests.

#### Done Notes

Planned; not started.

## Next Task Proposal

Next: implement `030_002` to normalize UTC timestamp storage/query boundaries
and project lean read models. The cursor and cache seams from `030_001` are the
verified foundation for that persistence task.
