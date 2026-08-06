---
phase: 028
title: Backend Boundary Refactor and Optimization
status: active
created_at: 2026-08-03
updated_at: 2026-08-06
current_task: 028_005
task_count: 12
done_count: 4
depends_on:
  - 027
---

# Phase 028 Summary

## Phase Goal

Refactor and optimize the backend around reusable hosting, persistence,
messaging, observability, schema, contract, and security boundaries without
changing public API behavior or business semantics.

## Phase Done Criteria

- The demo API does not duplicate endpoint mapping or backend business logic
  already owned by reusable projects.
- Shared hosting remains the canonical endpoint composition boundary.
- Hot read paths use seek-friendly pagination, bounded materialization, and
  cache/queue protection appropriate to their workload.
- Database, cache, and background-queue behavior has actionable latency,
  failure, retry, and backpressure telemetry.
- Large composition and service files are split into cohesive modules while
  preserving dependency direction and public contracts.
- Options, logging, schema evolution, tenant isolation, event delivery,
  contracts, hosting, and security responsibilities follow explicit boundaries.
- Focused tests and the full solution verification stay green after every task.

## Scope

In:

- Consolidating duplicated API and reusable hosting endpoint mappings.
- Maintaining one provider-neutral messaging boundary.
- Backend performance, observability, composition, configuration, persistence,
  tenancy, event-delivery, package, contract, and security-boundary work.
- Preserving task goals, acceptance criteria, verification evidence, and done
  notes from the former phase 030.

Out:

- Public route or DTO behavior changes.
- Unrelated frontend redesign.
- Vendor-specific monitoring adoption without evidence.
- Authentication algorithm or permission-semantic changes.

## Task Index

| Task | Title | Category | Status | Done At |
|---|---|---|---|---|
| 028_001 | Consolidate API endpoint mapping boundary | Boundary | done | 2026-08-03 |
| 028_002 | Extract provider-neutral queue library with memory and RabbitMQ adapters | Boundary | done | 2026-08-03 |
| 028_003 | Replace offset hot paths and protect cache misses | Performance | done | 2026-08-03 |
| 028_004 | Normalize UTC timestamps and project lean read models | Performance | done | 2026-08-04 |
| 028_005 | Add observability, backpressure, and bounded retry metrics | Performance | planned | |
| 028_006 | Split large services and composition modules | Convention | planned | |
| 028_007 | Unify options binding and structured logging conventions | Convention | planned | |
| 028_008 | Introduce versioned schema migration and compatibility boundaries | Architecture | planned | |
| 028_009 | Add tenant-aware audit and durable event delivery boundaries | Architecture | planned | |
| 028_010 | Split hosting/package boundaries and session responsibilities | Architecture | planned | |
| 028_011 | Extract stable contracts package | Architecture | planned | |
| 028_012 | Reassess the security package boundary | Architecture | planned | |

## Current Task

`028_004` is complete. `028_005` is the next planned task and remains
unstarted until explicitly selected.

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

### 028_003 - Replace offset hot paths and protect cache misses

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

### 028_004 - Normalize UTC timestamps and project lean read models

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

Completed on 2026-08-04.

Implemented:

- Added a shared EF value converter that persists every `DateTimeOffset` as a
  UTC `DateTime` provider value while preserving `DateTimeOffset` in domain and
  public contracts.
- Replaced SQLite date-range raw SQL and timestamp `.ToString()` ordering in
  short-link, click, and audit repositories with direct range/order predicates.
- Isolated the remaining SQLite-specific created-cursor SQL used only for the
  lexicographic code tie-breaker behind one repository method.
- Added lean persistence read models for short-link lists, recent clicks, and
  audit pages so queries omit idempotency and base update metadata not required
  by the response.
- Added an idempotent SQLite compatibility upgrade for legacy timestamp strings
  containing `Z` or explicit offsets, and wired it into built-in host startup.
- Documented UTC storage behavior and the custom-host compatibility call in the
  root README.

Verification:

- Infrastructure tests passed: 50/50, including provider mapping, legacy
  `+07:00` normalization, direct timestamp SQL, and lean projection assertions.
- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal
  --disable-build-servers` passed with 0 warnings and 0 errors.
- Full solution tests passed: 229/229.
- Clean consumer package smoke `1.0.6` passed create/detail/redirect/delete and
  post-delete behavior (`201/200/302/200/404`).
- Release dry-run passed with `Published: false`.

### 028_005 - Add observability, backpressure, and bounded retry metrics

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

### 028_006 - Split large services and composition modules

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

In: split `ShortLinkService` behind focused create, resolve, read, and management
collaborators while retaining a compatibility facade; split hosting registrations
by persistence, security, caching, messaging, and validation; split endpoint
mappings by feature; move EF mappings into `IEntityTypeConfiguration<TEntity>`
classes; group response contracts by feature; and rename transport-neutral Core
request types such as `AuditLogEndpointRequest` so Core does not expose HTTP
terminology. Remove unused inherited paging/filter members where compatibility
evidence permits.

Out: business rule changes and public route/DTO changes.

#### Acceptance Criteria

- Each extracted module has one clear responsibility and stable DI lifetime.
- No circular dependency or duplicate registration is introduced.
- Public routes, DTOs, and domain behavior remain unchanged.
- Existing `IShortLinkService` consumers remain source-compatible through a
  facade or an explicitly documented migration path.
- Core request types contain no `Endpoint` naming or unused HTTP-only state.
- File-size and namespace conventions are documented or enforced where useful.

#### Foundation for Next Step

The codebase has smaller seams for applying unified options and logging rules.

#### Affected Files

- `src/ShortenLink.Application/Services/ShortLinkService.cs`
- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `shared/ShortenLink.Hosting/ShortenLinkEndpointMappings.cs`
- `src/ShortenLink.Infrastructure/Persistence/ShortLinkDbContext.cs`
- `src/ShortenLink.Application/Contracts/Responses/ApplicationResponses.cs`
- `src/ShortenLink.Core/Contracts/Requests/AuditLogEndpointRequest.cs`

#### Verification

- Architecture/build checks and focused endpoint/service tests.
- Full solution build and tests.

#### Done Notes

Planned; not started.

### 028_007 - Unify options binding and structured logging conventions

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

### 028_008 - Introduce versioned schema migration and compatibility boundaries

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

### 028_009 - Add tenant-aware audit and durable event delivery boundaries

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

### 028_010 - Split hosting/package boundaries and session responsibilities

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

### 028_011 - Extract stable contracts package

#### Step Goal

Create a dependency-light `ShortenLink.Contracts` package for stable requests,
responses, pagination, and error payloads without pulling the application
pipeline, domain services, host integration, or persistence stack.

#### Scope

In: add `shared/ShortenLink.Contracts`; move transport-neutral DTOs; keep domain
mapping in Application/adapters; update references, package scripts, contract
tests, README integration guidance, and consumer smoke. Out: domain entities,
validators, handlers, EF entities, ASP.NET attributes, and authorization state.

#### Acceptance Criteria

- The package has no dependency on Application, Infrastructure, Hosting,
  FluentValidation, mediator, EF Core, or demo API projects.
- Contracts contain no domain mapping methods or ASP.NET-specific types.
- Existing JSON shapes remain compatible and Application owns DTO mapping.
- Release dry-run and clean consumer smoke include the package.

#### Foundation for Next Step

An independent DTO boundary makes the remaining Security dependencies measurable
before deciding whether Security warrants a package family.

#### Affected Files

- `src/ShortenLink.Core/Contracts/`
- `src/ShortenLink.Application/Contracts/`
- `src/ShortenLink.Application/Features/`
- `src/ShortenLink.Api/Endpoints/`
- `shared/ShortenLink.Contracts/`
- Project files, release scripts, README, and contract tests.

#### Verification

- Dependency graph and serialization/contract tests.
- Full solution build and tests.
- Release dry-run and clean consumer package smoke.

#### Done Notes

Planned; not started.

### 028_012 - Reassess the security package boundary

#### Step Goal

Evaluate Security after the service, hosting, persistence, and contracts seams
are split; either extract an acyclic package family or record a durable ADR that
defines why extraction is deferred and what would justify it.

#### Scope

In: classify Security code into domain, application, persistence, and host
adapters; inspect redirect-only/external-host dependency graphs; extract only
when each project has one layer and clear consumer. Out: authentication
algorithm, permission semantic, route, ownership, or sharing behavior changes.

#### Acceptance Criteria

- Security responsibilities and dependency direction are documented.
- Redirect-only consumers avoid unnecessary demo session/admin components.
- Extracted projects are independently buildable, packable, and tested; or the
  ADR records concrete blockers and future extraction criteria.
- Existing authentication and authorization behavior remains unchanged.

#### Foundation for Next Step

Phase 028 leaves explicit service, persistence, contracts, hosting, and security
boundaries that later work can extend without reopening these decisions.

#### Affected Files

- `src/ShortenLink.Core/Security/`
- `src/ShortenLink.Application/Features/Security/`
- `src/ShortenLink.Infrastructure/Repositories/*Security*.cs`
- `shared/ShortenLink.Hosting/*Security*.cs`
- Project files, architecture docs/ADR, and security tests.

#### Verification

- Dependency graph and focused security tests.
- Full solution build and tests.
- Pack and consumer smoke when extraction occurs.

#### Done Notes

Planned; not started.

## Compaction Provenance

Tasks formerly stored in `.okf/phase/030/PHASE_SUMMARY.md` were merged into
this phase on 2026-08-06. The original ids remain searchable through this map:

| Original ID | Phase 028 ID |
|---|---|
| 030_001 | 028_003 |
| 030_002 | 028_004 |
| 030_003 | 028_005 |
| 030_004 | 028_006 |
| 030_005 | 028_007 |
| 030_006 | 028_008 |
| 030_007 | 028_009 |
| 030_008 | 028_010 |
| 030_009 | 028_011 |
| 030_010 | 028_012 |

Source before compaction: `.okf/phase/030/PHASE_SUMMARY.md`.

## Next Task Proposal

Next: implement `028_005` to add observability, backpressure, and bounded retry
metrics on top of the completed cursor, cache, UTC timestamp, and lean read
boundaries from `028_003` and `028_004`.

Planned refactor sequence after the performance and observability foundations:

1. `028_006` creates internal seams and removes misleading request/endpoint
   naming without changing public behavior.
2. `028_010` separates ASP.NET Core hosting, workers, and session/security
   orchestration responsibilities.
3. `028_011` extracts the transport-neutral `ShortenLink.Contracts` package.
4. `028_012` makes an evidence-based Security package decision after those
   dependencies are no longer entangled.
