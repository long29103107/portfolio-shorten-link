---
phase: 028
title: Backend Boundary Refactor and Optimization
status: active
created_at: 2026-08-03
updated_at: 2026-08-06
current_task: 028_011
task_count: 12
done_count: 10
depends_on:
  - 027
---

# Phase 028 Summary

## Phase Goal

Refactor the backend into clear, reusable code and package boundaries while
preserving every existing public route, contract, business rule, persistence
semantic, and runtime behavior. This phase is for code quality, convention,
dependency direction, and structural reuse; it does not add product features.

## Phase Done Criteria

- Existing API, DTO, domain, persistence, queue, authorization, and session
  behavior remains compatible.
- Large services and composition files are split into cohesive modules with
  explicit responsibilities and stable lifetimes.
- Options, logging, persistence, messaging, hosting, and security conventions
  are consistent and testable without process-global coupling.
- A separate library is created only when a concrete reusable consumer and an
  acyclic dependency boundary justify it.
- No new route, payload field, business policy, authentication algorithm, retry
  policy, telemetry product, or user-facing capability is introduced.
- Focused architecture/build/test verification remains green after each task.

## Scope

In:

- Refactoring code structure, naming, dependency direction, conventions, and
  module/package boundaries.
- Extracting reusable libraries only when reuse is demonstrated by at least two
  legitimate consumers or a clearly documented external-host contract.
- Adding characterization tests, architecture checks, and ADRs needed to prove
  behavior preservation.

Out:

- New product features, public API changes, schema capabilities, queue
  semantics, authorization semantics, or monitoring-vendor adoption.
- Broad rewrites without a measurable ownership or reuse benefit.

## Task Index

| Task | Title | Category | Status | Done At |
|---|---|---|---|---|
| 028_001 | Consolidate API endpoint mapping boundary | Boundary | done | 2026-08-03 |
| 028_002 | Extract provider-neutral queue library with memory and RabbitMQ adapters | Boundary | done | 2026-08-03 |
| 028_003 | Replace offset hot paths and protect cache misses | Performance | done | 2026-08-03 |
| 028_004 | Normalize UTC timestamps and project lean read models | Performance | done | 2026-08-04 |
| 028_005 | Audit cross-cutting diagnostics and operational seams | Refactor | done | 2026-08-06 |
| 028_006 | Split large services and composition modules | Refactor | done | 2026-08-06 |
| 028_007 | Normalize options and structured logging conventions | Convention | done | 2026-08-06 |
| 028_008 | Isolate schema and persistence compatibility boundaries | Refactor | done | 2026-08-06 |
| 028_009 | Refactor audit and event-delivery ownership boundaries | Refactor | done | 2026-08-06 |
| 028_010 | Split hosting, package, and session responsibilities | Architecture | done | 2026-08-06 |
| 028_011 | Extract stable contracts only when reuse is proven | Architecture | planned | |
| 028_012 | Audit and document the security package boundary | Architecture | planned | |

## Current Task

`028_011` is the next planned stable-contract boundary task. The hosting,
package, and session responsibility refactor in `028_010`, the audit and
event-delivery ownership refactor in `028_009`, and the schema/persistence
compatibility refactor in `028_008` are complete; the options and structured-
logging convention refactor in `028_007` and the diagnostics seam audit in
`028_005` remain complete without adding new telemetry or monitoring behavior.

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

### 028_005 - Audit cross-cutting diagnostics and operational seams

#### Step Goal

Inventory existing diagnostics, timing hooks, and operational extension points,
then refactor their ownership and naming without introducing new metrics,
events, dashboards, or monitoring dependencies.

#### Scope

In: characterize current diagnostic behavior; remove duplicated or ad-hoc
diagnostic helpers; centralize stable internal seams; preserve existing log
levels, event text where externally consumed, and disabled-path overhead.

Out: new telemetry signals, retry/backpressure policy changes, vendor
integration, dashboards, and new operational features.

#### Acceptance Criteria

- Existing diagnostics have one clear owner and no duplicate production paths.
- Refactoring does not add a new observable contract or change business flow.
- Sensitive values remain excluded and existing tests continue to pass.
- Any future instrumentation opportunity is recorded as a follow-up, not built.

#### Foundation for Next Step

The codebase has a documented diagnostic boundary that can be considered while
splitting large services and composition modules.

#### Affected Files

- Existing hosting, application, infrastructure, and worker diagnostic helpers.
- Focused architecture and characterization tests.
- Phase documentation for deferred instrumentation opportunities.

#### Verification

- Focused diagnostics/log-capture tests.
- Architecture/build verification for affected projects.
- Full solution verification when shared hosting code changes.

#### Done Notes

Done on 2026-08-06.

Implemented a refactor-only diagnostics seam audit:

- Centralized the existing mediator `Trace.WriteLine` messages in the internal
  `RequestDiagnostics` seam while preserving message text and failure behavior.
- Kept redirect `ActivitySource`/`Meter` ownership in Core unchanged; no new
  metrics, events, dashboards, exporters, or observable contract were added.
- Added characterization tests for successful and failed request diagnostics,
  including an assertion that exception payload text is never logged.
- Recorded structured logging as the explicitly deferred concern for `028_007`.

Verification:

- Focused application tests passed: 20/20.
- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
  passed with 0 warnings and 0 errors.
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
  passed: 230/230 tests.

### 028_006 - Split large services and composition modules

#### Step Goal

Split the oversized application service and host dependency-registration module
into cohesive partial modules while preserving public behavior and dependency
direction.

#### Scope

In: extract focused service collaborators behind the existing compatibility
facade; split hosting registrations by mediator, rate limiting, cache, queue,
and validation concern; preserve the existing endpoint, EF, and response
contract boundaries for later focused tasks.

Out: business-rule changes, route/DTO changes, new abstractions without a
consumer, and feature additions.

#### Acceptance Criteria

- Each extracted module has one responsibility and an explicit DI lifetime.
- Existing consumers remain source-compatible through a facade or documented
  migration path.
- No duplicate registration, circular dependency, route change, or response
  shape change is introduced.
- Refactor characterization tests prove behavior parity.

#### Foundation for Next Step

Smaller modules expose consistent seams for options and logging convention
cleanup.

#### Affected Files

- `src/ShortenLink.Application/Services/ShortLinkService.cs`
- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `src/ShortenLink.Application/Services/ShortLinkService.cs`
- New service and hosting partial module files.
- Focused architecture and service tests.

#### Verification

- Architecture/dependency graph checks.
- Focused endpoint, service, and persistence tests.
- Full solution build and tests.

#### Done Notes

Done on 2026-08-06.

Implemented a behavior-preserving structural split without adding features:

- Split `ShortLinkService` into focused listing, operations, and resolution
  partial modules while retaining the same public interfaces and constructor.
- Split hosting service registration into focused mediator, rate-limiting,
  cache, queue, and validation partial modules.
- Kept the main service-registration and service files as thin composition
  boundaries; no route, DTO, business-rule, DI lifetime, or runtime behavior
  changed.
- Followed up by grouping `ShortenLink.Hosting` into responsibility folders
  (`Registration`, `Endpoints`, `Options`, `Security`, `Caching`, `Messaging`,
  `Persistence`, `Health`, `Context`, and `Policies`), shortening implementation
  file/class names while retaining public option and interface contracts.

Verification:

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
  passed with 0 warnings and 0 errors.
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
  passed: 228/228 tests.

### 028_007 - Normalize options and structured logging conventions

#### Step Goal

Refactor configuration and logging access so each feature has one canonical
binding path and stable structured conventions, without changing effective
configuration, log policy, or business behavior.

#### Scope

In: consolidate duplicate options binding; add validation around existing
configuration; replace ad-hoc production diagnostics with existing logging
abstractions; redact sensitive fields; preserve compatible keys and event
meaning.

Out: new configuration capabilities, retention/vendor changes, new log
consumers, and behavior changes driven by new options.

#### Acceptance Criteria

- Each feature has one canonical options source and test-replaceable dependency.
- Existing configuration keys remain compatible or have a documented mapping.
- Production diagnostics use stable structured logging without secrets or full
  payloads.
- Tests cover binding precedence and logging redaction.

#### Foundation for Next Step

Configuration and logging conventions are stable before persistence-boundary
refactors are reviewed.

#### Affected Files

- `shared/ShortenLink.Hosting/`
- `src/ShortenLink.Application/Behaviors/LoggingPipelineBehavior.cs`
- Feature option records, registration code, and tests.

#### Verification

- Options validation and logging-capture tests.
- Full solution build and tests.

#### Done Notes

Done on 2026-08-06.

Implemented a behavior-preserving options and logging convention refactor:

- Kept one canonical root `IOptions<ShortenLinkOptions>` binding with
  validation-on-start; cache registration now reads only the existing nested
  cache section and the health-check gate reads only its existing scalar key,
  avoiding duplicate root options materialization.
- Replaced the application `Trace.WriteLine` seam with host-neutral
  `IRequestLogger` callbacks and a hosting `StructuredRequestLogger` adapter
  using stable event ids 2001/2002.
- Structured diagnostics contain only request type, elapsed milliseconds, and
  exception type; request payloads and exception objects are never logged.
- Preserved existing configuration keys, effective provider/health-check
  behavior, exception propagation, and log meaning; no new capability was
  introduced.

Verification:

- Focused application tests passed: 20/20.
- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal
  --disable-build-servers` passed with 0 warnings and 0 errors.
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
  passed: 230/230 tests.

### 028_008 - Isolate schema and persistence compatibility boundaries

#### Step Goal

Refactor schema initialization, provider-specific SQL, and compatibility code
behind one explicit persistence boundary without adding tables, indexes, or
business capabilities.

#### Scope

In: isolate existing schema creation/upgrade helpers; make provider dialect
selection explicit; document current version/compatibility assumptions; add
characterization coverage for fresh and legacy databases.

Out: new schema features, new migration policy, business-table redesign, and
provider replacement.

#### Acceptance Criteria

- Fresh and existing supported databases retain the current effective schema.
- Provider-specific behavior is localized to persistence adapters.
- Startup/upgrade behavior remains idempotent and diagnosable.
- No public or domain behavior changes.

#### Foundation for Next Step

A stable persistence boundary makes audit and event ownership refactors
reviewable without mixing provider concerns into business code.

#### Affected Files

- `src/ShortenLink.Infrastructure/Persistence/`
- Database initialization and compatibility helpers.
- Provider contract and schema tests.

#### Verification

- Fresh/legacy SQLite and supported-provider compatibility tests.
- Schema/index assertions and full solution tests.

#### Done Notes

Done on 2026-08-06.

Implemented a behavior-preserving persistence compatibility boundary:

- Kept `ShortLinkDatabaseSchema` as the stable public facade while moving
  provider-specific SQL and upgrade routines into explicit SQLite and
  PostgreSQL dialect adapters.
- Added an explicit provider resolver with a safe no-op dialect for unsupported
  providers; the host continues to use the same `EnsureCreated` startup flow.
- Preserved the existing audit-event, expiration-checkpoint,
  tenant/idempotency, and UTC timestamp compatibility SQL, including all
  table/index names and defaults.
- Documented the current `EnsureCreated` compatibility assumptions and kept
  the boundary idempotent without introducing migrations, tables, indexes, or
  business capabilities.
- Added fresh-database idempotency characterization coverage in addition to
  the existing legacy SQLite schema tests.

Verification:

- Focused infrastructure tests passed: 51/51.
- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal
  --disable-build-servers` passed with 0 warnings and 0 errors.
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
  passed: 231/231 tests.

### 028_009 - Refactor audit and event-delivery ownership boundaries

#### Step Goal

Clarify ownership and dependency direction for existing audit, click, and
queue-delivery code while preserving current payloads, retry behavior, and
tenant semantics.

#### Scope

In: extract repository/dispatcher/worker seams; isolate idempotency and
ownership checks already present; add characterization tests; create a reusable
library only if existing consumers demonstrably need the same boundary.

Out: new outbox semantics, tenant model changes, retry/dead-letter policy
changes, event schema changes, and new delivery guarantees.

#### Acceptance Criteria

- Audit and event code has explicit ownership and no cross-layer leakage.
- Existing queue behavior, payloads, idempotency, and tenant visibility remain
  unchanged.
- Any extracted library has an acyclic dependency graph and a real second
  consumer; otherwise the seam stays internal.
- Failure paths remain covered by focused tests.

#### Foundation for Next Step

Event ownership is explicit before hosting and session responsibilities are
split into reusable boundaries.

#### Affected Files

- Audit contracts/entities/repositories.
- Existing queue adapters and worker composition.
- Application/infrastructure integration tests.

#### Verification

- Focused audit, queue, idempotency, and tenant-scope tests.
- Dependency graph and full solution verification.

#### Done Notes

Done on 2026-08-06.

Implemented a behavior-preserving event-delivery ownership refactor:

- Centralized queue consumption, scoped dependency resolution, acknowledgement,
  cancellation handling, and poison-message rejection in the internal
  `MessageDeliveryWorker<TMessage>` boundary.
- Reduced `AuditWorker` and `ClickWorker` to their owned responsibilities:
  mapping each payload to its repository operation and retaining the existing
  failure diagnostics.
- Preserved audit/click payloads, tenant propagation, queue provider behavior,
  ack/reject semantics, cancellation behavior, idempotency boundaries, and
  existing log meaning. No new retry, outbox, dead-letter, or delivery
  guarantee was introduced.
- Kept the seam internal because both consumers are in the hosting layer and
  no second external package consumer justifies extraction.

Verification:

- Focused application tests passed: 20/20.
- Focused messaging tests passed: 6/6.
- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal
  --disable-build-servers` passed with 0 warnings and 0 errors.
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
  passed: 231/231 tests.

### 028_010 - Split hosting, package, and session responsibilities

#### Step Goal

Separate reusable ASP.NET Core integration, worker composition, and session
token/permission/orchestration responsibilities without changing algorithms or
public contracts.

#### Scope

In: extract host adapters and focused security-session interfaces; remove
unnecessary demo-host dependencies; create a separate library only where a
real external or second internal host can consume it.

Out: authentication algorithm changes, permission semantic changes, route
changes, and packages created solely for theoretical reuse.

#### Acceptance Criteria

- Business projects remain host-agnostic and the dependency graph is acyclic.
- Session token, permission, and orchestration responsibilities have explicit
  interfaces and lifetimes.
- Existing authentication/authorization/API tests remain green.
- Extracted libraries are independently buildable only when justified.

#### Foundation for Next Step

Hosting and session seams are stable enough to evaluate a contracts package
without importing host or application implementation details.

#### Affected Files

- `shared/ShortenLink.Hosting/`
- Session/authorization adapters and related project files.
- Hosting and security tests.

#### Verification

- Dependency/build graph checks.
- Focused authentication, authorization, and host integration tests.
- Full solution verification.

#### Done Notes

Done on 2026-08-06.

Implemented a behavior-preserving hosting and session boundary refactor:

- Moved the application session adapter from the demo API into
  `ShortenLink.Hosting`, making session orchestration a reusable host concern
  while keeping the existing `ISecuritySessionService` contract and response
  mapping unchanged.
- Registered the adapter in hosting with the existing scoped lifetime and
  `TryAdd` override behavior; API `Program.cs` now only composes the host and
  endpoints.
- Kept `IShortenLinkUserSessionService`, authorization services, token format,
  permission catalog, authentication algorithms, route mappings, and public
  contracts unchanged.
- Did not extract another package: the repository has one concrete API host and
  no second consumer justifying a new package boundary. The existing Hosting,
  Application, Core, Infrastructure, and Messaging dependency graph remains
  acyclic.
- Added a DI characterization assertion for the host-owned session adapter.

Verification:

- Focused host DI test passed: 1/1.
- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal
  --disable-build-servers` passed with 0 warnings and 0 errors.
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
  passed: 231/231 tests.

### 028_011 - Extract stable contracts only when reuse is proven

#### Step Goal

Audit request/response/pagination contracts and extract a dependency-light
contracts library only if at least two concrete consumers or an external-host
contract justify the boundary; otherwise document why extraction is deferred.

#### Scope

In: classify transport-neutral DTOs; remove application/domain mapping from
contracts; preserve JSON shapes; update references and package metadata only
when extraction is justified.

Out: domain entities, validators, handlers, ASP.NET attributes, authorization
state, DTO redesign, and speculative package creation.

#### Acceptance Criteria

- The decision is evidence-based and recorded in an ADR or task notes.
- If extracted, the library has no Application, Infrastructure, Hosting, EF, or
  ASP.NET dependency and existing JSON shapes remain compatible.
- If deferred, the blocker and concrete extraction criteria are documented.
- Consumer/build/package checks pass for the selected outcome.

#### Foundation for Next Step

Contract ownership is explicit before the final security-boundary audit.

#### Affected Files

- `src/ShortenLink.Core/Contracts/`
- `src/ShortenLink.Application/Contracts/`
- API adapters, project files, package metadata, and contract tests.

#### Verification

- Dependency graph and serialization tests.
- Full solution build/tests.
- Package/consumer smoke only if a package is extracted.

#### Done Notes

Planned; not started.

### 028_012 - Audit and document the security package boundary

#### Step Goal

Classify existing security code by layer and decide whether extraction is
justified by a concrete reusable consumer, preserving all authentication,
authorization, redirect, ownership, and sharing behavior.

#### Scope

In: dependency graph audit; classify Core/Application/Infrastructure/Hosting
responsibilities; document redirect-only and external-host dependencies; extract
only a proven acyclic reusable seam.

Out: authentication algorithms, permission semantics, route behavior, ownership
rules, sharing behavior, and speculative package families.

#### Acceptance Criteria

- Security responsibilities and dependency direction are documented.
- Redirect-only consumers avoid unnecessary admin/session dependencies where
  the current graph permits a behavior-preserving refactor.
- An extracted package is independently buildable/testable only when justified;
  otherwise the ADR records concrete blockers and future criteria.
- Existing security tests remain green.

#### Foundation for Next Step

Phase 028 leaves explicit, behavior-preserving boundaries for future work
without requiring another broad security rewrite.

#### Affected Files

- `src/ShortenLink.Core/Security/`
- `src/ShortenLink.Application/Features/Security/`
- Security repositories, hosting adapters, project files, ADRs, and tests.

#### Verification

- Dependency graph and focused security tests.
- Full solution build/tests.
- Pack and consumer smoke only if extraction occurs.

#### Done Notes

Planned; not started.

## Compaction Provenance

Tasks formerly stored in `.okf/phase/030/PHASE_SUMMARY.md` were merged into
this phase on 2026-08-06. Their task ids were retained while their scopes were
audited and rewritten to be refactor-only. The former feature-oriented scopes
are intentionally superseded by the task notes above.

Source before compaction: `.okf/phase/030/PHASE_SUMMARY.md`.

## Next Task Proposal

Next: implement `028_011`, the stable contracts boundary audit. Extract a
package only when at least two concrete consumers or an external-host contract
justify it; otherwise document the deferral.
