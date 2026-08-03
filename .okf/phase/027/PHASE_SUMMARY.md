---
phase: 027
title: Reliable Integration Workflows
status: complete
created_at: 2026-07-31
updated_at: 2026-08-02
current_task:
task_count: 8
done_count: 8
depends_on:
  - 026
---

# Phase 027 Summary

## Phase Goal

Support retries, automation, and larger integrations without changing the
simple local developer path.

## Phase Done Criteria

- Retried create requests do not create unintended duplicate links when an
  idempotency key is supplied.
- Bulk import/export work can build on a stable idempotency and error boundary.
- Tenant-aware and retention work remain isolated behind later task contracts.

## Scope

In:

- Optional idempotent create requests and provider-neutral persistence keys.
- Bulk import/export contracts with dry-run and streaming boundaries.
- Optional tenant partition and expiration/retention hooks.

Out:

- Full bulk import/export implementation, tenant partitioning, and retention
  workers; those require later tasks in this phase.

## Task Index

| Task | Title | Status | Done At |
|---|---|---|---|
| 027_001 | Idempotent create request contract and persistence key boundary | done | 2026-07-31 |
| 027_002 | Bulk import dry-run contract and per-item failure boundary | done | 2026-07-31 |
| 027_003 | Streaming bulk import execution and per-item persistence results | done | 2026-07-31 |
| 027_004 | Streaming bulk export read boundary | done | 2026-08-01 |
| 027_005 | Optional tenant partition context and repository boundary | done | 2026-08-01 |
| 027_006 | Tenant-aware resolve, cache, and analytics boundary | done | 2026-08-02 |
| 027_007 | Expiration and retention hook boundary | done | 2026-08-02 |
| 027_008 | Safe expiration execution boundary | done | 2026-08-02 |

## Current Task

No task is active. `027_008` is complete and Phase 027 is closed.

## Completed Notes

`027_001` completed the durable create replay boundary.

`027_002` completed bounded dry-run validation and the per-item import error
boundary.

`027_003` completed retry-safe streaming import execution with per-item
persistence results and audit-safe replays.

`027_004` completed a bounded, permission-aware export stream over safe recent
link records without exposing provider or persistence details.

`027_005` completed trusted host tenant context, fail-closed provider opt-in,
tenant-scoped create/import/list/export authorization, and tenant-aware
idempotency persistence.

## Task Notes

### 027_008 - Safe expiration execution boundary

#### Step Goal

Consume the read-only expiration batch contract through an explicitly
triggered, non-destructive execution path that resumes from a durable
checkpoint and hands expired cache entries to a tenant-safe invalidation seam.

#### Dependency

- `027_007` deterministic expiration evaluation, stable cursors, and
  versioned expiration metadata.
- Existing provider-neutral cache and EF compatibility boundaries.

#### Scope

In:

- Add durable, tenant-scoped expiration checkpoints with stable cursor and
  evaluation metadata.
- Add an explicit execution service and API trigger that evaluates one bounded
  batch, advances the checkpoint only after successful handoff, and never
  deletes or deactivates persistence records.
- Add a tenant-safe cache invalidation handoff for expired evaluations.
- Add Core/application/infrastructure/API coverage and consumer documentation.

Out:

- Automatic scheduling, hosted workers, destructive deletion, archive storage,
  legal holds, and retention administration.

#### Acceptance Criteria

- An explicit trigger can execute a bounded batch with a supplied evaluation
  instant and optional tenant partition.
- Checkpoints are durable, tenant-scoped, and resume with stable cursors;
  failed evaluation or invalidation does not advance the checkpoint.
- Expired records produce cache invalidation handoffs without mutating the
  short-link, analytics, or share persistence state.
- Tenant-A execution cannot read or invalidate tenant-B cache entries, while
  the unscoped default path remains compatible.
- The endpoint requires the existing status permission and does not register
  a scheduler or background worker.

#### Foundation for Next Step

The system has an explicit, retryable expiration execution boundary with
durable progress and cache handoff semantics that a later scheduler can call
without moving cleanup policy into request-time redirects.

#### Affected Files

- `src/ShortenLink.Core/Abstractions/`
- `src/ShortenLink.Core/Contracts/Expiration/`
- `src/ShortenLink.Application/Services/`
- `src/ShortenLink.Infrastructure/Persistence/`
- `src/ShortenLink.Infrastructure/Repositories/`
- `shared/ShortenLink.Hosting/`
- `src/ShortenLink.Api/Endpoints/`
- `tests/ShortenLink.Core.Tests/`
- `tests/ShortenLink.Application.Tests/`
- `tests/ShortenLink.Infrastructure.Tests/`
- `tests/ShortenLink.Api.Tests/`
- `README.md`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Focused checkpoint, retry, tenant-isolation, cache-handoff, and no-mutation
  tests.

#### Done Notes

Done. Added the explicitly triggered `POST
/api/short-links/expiration/execute` path with status-permission authorization,
trusted tenant propagation, durable EF/SQLite/PostgreSQL checkpoints, stable
cursor resume behavior, and a tenant-safe cache invalidation handoff. Failed
handoffs do not advance checkpoints, and the execution path never deletes or
deactivates links. Added Core/application/infrastructure/API coverage and
documented the manual trigger. Verification passed with a warning-free
solution build and all 213 solution tests.

### 027_007 - Expiration and retention hook boundary

#### Step Goal

Define provider-neutral expiration notifications, retention policy seams, and
bounded batch-processing contracts that build on the tenant-aware lifecycle
boundary without introducing a scheduler, destructive cleanup worker, or
retention policy execution yet.

#### Dependency

- `027_006` tenant-aware resolve, cache invalidation, analytics, and lifecycle
  event boundaries.
- Existing expiration fields, lifecycle event sink, repository, and bounded
  import/export processing conventions.

#### Scope

In:

- Add an explicit expiration/retention evaluation result and policy contract.
- Define provider-neutral batch input/output boundaries with stable cursors,
  limits, per-item outcomes, and tenant-safe identifiers.
- Add versioned, secret-safe expiration lifecycle event metadata and hooks for
  cache invalidation consumers.
- Document how a future hosted worker can consume the boundary without changing
  request-time redirect behavior.
- Add Core/application/infrastructure contract tests, including default and
  tenant-partitioned records.

Out:

- Scheduler registration, hosted background workers, automatic deletion,
  destructive cleanup, archive storage, legal holds, and user-facing retention
  administration.

#### Acceptance Criteria

- Expiration evaluation is deterministic at a supplied `TimeProvider` instant
  and does not mutate persistence, cache, or analytics state.
- Batch contracts clamp limits, preserve stable ordering/cursors, and report
  per-item skipped/expired/retained outcomes without aborting the batch.
- Tenant identifiers are carried only through trusted, provider-neutral hooks;
  a tenant-A record cannot be evaluated or invalidated as tenant B.
- Expiration events are versioned and secret-safe, preserve existing lifecycle
  event compatibility, and provide enough metadata for a future worker to
  invalidate the correct cache partition.
- Existing public redirects, unscoped providers, and default single-tenant
  behavior remain source and behavior compatible.

#### Foundation for Next Step

The phase will have a safe, non-destructive expiration/retention boundary that
can be consumed by a later scheduled worker or cleanup implementation without
moving policy decisions into HTTP request paths.

#### Affected Files

- `src/ShortenLink.Core/Abstractions/`
- `src/ShortenLink.Core/Contracts/`
- `src/ShortenLink.Core/Events/`
- `src/ShortenLink.Application/Services/`
- `src/ShortenLink.Application/Features/ShortLinks/`
- `src/ShortenLink.Infrastructure/Repositories/`
- `shared/ShortenLink.Hosting/`
- `tests/ShortenLink.Core.Tests/`
- `tests/ShortenLink.Application.Tests/`
- `tests/ShortenLink.Infrastructure.Tests/`
- `README.md`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Focused deterministic-time, batch-bound, tenant-isolation, event-safety, and
  no-mutation contract tests.

#### Done Notes

Done. Added deterministic, read-only expiration evaluation with retention
policy outcomes, stable expiration cursors, bounded batch limits, and tenant
partition filtering. Added the optional `IShortLinkExpirationRepository`
capability, application evaluation service, fail-open versioned expiration event
hook, built-in DI registration, SQLite/EF coverage, and documentation. No
scheduler, worker, deletion, or request-time mutation was introduced.
Verification passed with a warning-free solution build, all 209 solution tests,
and successful Release package creation; the existing non-packable API warning
remains.

### 027_006 - Tenant-aware resolve, cache, and analytics boundary

#### Step Goal

Carry the trusted optional tenant context through redirect resolution, cache
lookup/invalidation, analytics reads, and lifecycle operations so tenant-aware
consumers cannot cross partition boundaries while the default public
single-tenant redirect path remains unchanged.

#### Dependency

- `027_005` trusted tenant actor, access scope, persistence field, and
  fail-closed provider capability.
- Existing Core cache, analytics, lifecycle, and redirect event contracts.

#### Scope

In:

- Add tenant-aware resolve and lifecycle service seams using the existing
  trusted request/access context.
- Add tenant partition keys or equivalent provider-neutral cache boundaries.
- Scope analytics persistence/query operations to the current tenant.
- Preserve tenant identity in safe cache/event/analytics contracts without
  exposing it in public link responses.
- Add API/application/infrastructure coverage for tenant isolation, cache
  invalidation, redirect behavior, and analytics reads.

Out:

- Tenant administration, billing, tenant discovery, custom domains, expiration
  workers, cleanup scheduling, and retention policy execution.

#### Acceptance Criteria

- Tenant-aware resolve cannot return a link from another tenant, including on a
  cache hit; the default no-tenant redirect behavior is unchanged.
- Update, activate, deactivate, delete, and cache invalidation use the same
  tenant boundary as list/detail authorization.
- Tenant-aware analytics writes and reads cannot cross partitions, while
  existing unscoped analytics consumers remain source compatible.
- Cache keys and provider contracts prevent a tenant-A record from satisfying
  a tenant-B lookup or invalidation request.
- Lifecycle and redirect events remain versioned and secret-safe, with tenant
  context represented only where the consumer contract explicitly allows it.

#### Foundation for Next Step

All request-time and operational paths share one tenant boundary. Later work can
add optional expiration events, cleanup, and retention hooks without reworking
authorization, cache, or analytics isolation.

#### Affected Files

- `src/ShortenLink.Core/Abstractions/`
- `src/ShortenLink.Core/Contracts/`
- `src/ShortenLink.Core/Events/`
- `src/ShortenLink.Application/Features/ShortLinks/`
- `src/ShortenLink.Application/Features/Analytics/`
- `src/ShortenLink.Infrastructure/Repositories/`
- `src/ShortenLink.Infrastructure/Persistence/`
- `src/ShortenLink.Hosting/`
- `tests/ShortenLink.Core.Tests/`
- `tests/ShortenLink.Application.Tests/`
- `tests/ShortenLink.Infrastructure.Tests/`
- `tests/ShortenLink.Api.Tests/`
- `README.md`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Focused tenant resolve/cache, analytics isolation, lifecycle authorization,
  event safety, and default-path compatibility tests.

#### Done Notes

Done. Added the optional trusted tenant resolver seam, tenant-specific cache
keys/capability with safe bypass for legacy providers, tenant verification after
database reads, and tenant-scoped cache invalidation across lifecycle mutations.
Click records and persistence now carry tenant partition data; analytics uses an
explicit tenant repository capability while preserving unscoped source
compatibility. Lifecycle events remain versioned and secret-safe with an
optional non-secret tenant identifier. Added SQLite/PostgreSQL compatibility
handling, README guidance, and Core, infrastructure, and API isolation tests.
Verification passed with a warning-free solution build, all 204 solution tests,
and successful Release package creation (the existing non-packable API warning
remains).

### 027_005 - Optional tenant partition context and repository boundary

#### Step Goal

Add an opt-in tenant partition hook that carries a trusted host tenant identity
through create, import, list, export, authorization, and built-in persistence
while leaving the default single-tenant path unchanged.

#### Dependency

- `027_001` durable idempotency and provider capability boundary.
- `027_003` import execution context.
- `027_004` permission-aware export access scope.

#### Scope

In:

- Add an optional tenant identifier to current-request actor, access scope,
  create request, domain, and persistence contracts.
- Require an explicit provider capability before tenant-scoped operations run.
- Partition built-in list/export reads and authorization checks by tenant.
- Scope idempotency uniqueness and replay lookup by tenant.
- Preserve global short-code uniqueness for unambiguous public redirects.
- Add SQLite/model, Core, and API isolation coverage plus consumer guidance.

Out:

- Tenant/workspace administration, tenant discovery, billing, tenant-specific
  routes/domains, and migration of shares or audit history between tenants.

#### Acceptance Criteria

- Hosts opt in by returning a normalized tenant identifier from
  `ICurrentRequestContext`; the built-in context continues returning no tenant.
- Create and import persist the actor tenant, and list/export return only that
  tenant even for tenant-scoped administrators.
- Detail and mutation authorization rejects a link from another tenant before
  owner/share/Admin bypass rules are considered.
- Equal idempotency keys can create independent links in different tenants,
  while reuse inside one tenant still replays or conflicts deterministically.
- Tenant-scoped operations fail closed when a custom provider does not declare
  tenant support; the existing single-tenant provider contract remains source
  compatible.
- SQLite and PostgreSQL models use a tenant-aware idempotency index, while
  short codes remain globally unique.

#### Foundation for Next Step

Tenant identity and persistence isolation become stable opt-in hooks that later
cache partitioning, lifecycle, retention, and host adapters can reuse.

#### Affected Files

- `src/ShortenLink.Core/`
- `src/ShortenLink.Application/`
- `src/ShortenLink.Infrastructure/`
- `shared/ShortenLink.Hosting/`
- `tests/ShortenLink.Core.Tests/`
- `tests/ShortenLink.Infrastructure.Tests/`
- `tests/ShortenLink.Api.Tests/`
- `README.md`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Focused tenant default-compatibility, isolation, authorization, idempotency,
  schema, and provider-capability tests.

#### Done Notes

Done. Added optional normalized tenant identity to the request actor, access
scope, create request, domain entity, and built-in persistence model. Tenant
create/import operations now persist the actor partition; list/export and
detail/mutation authorization reject cross-partition access before Admin,
owner, or share rules. Added the explicit `IShortLinkTenantRepository`
capability so custom providers remain source compatible on the single-tenant
path and fail closed when tenant behavior is requested. Built-in SQLite and
PostgreSQL models now scope idempotency uniqueness by `(TenantId,
IdempotencyKey)` while preserving globally unique short codes, including an
idempotent SQLite compatibility upgrade. Verification passed with a
warning-free solution build, 10 focused tenant tests, all 202 solution tests,
and successful Release package creation for all five packable projects. Pack
reported only the existing non-packable API and missing-readme warnings.

### 027_004 - Streaming bulk export read boundary

#### Step Goal

Expose a bounded, permission-aware async export stream over recent accessible
short links without coupling HTTP consumers to the configured persistence
provider.

#### Dependency

- `027_001` provider-neutral recent-link and identity boundaries.
- `027_003` bounded async-enumerable integration workflow conventions.

#### Scope

In:

- Add a safe export record contract and bounded export limits.
- Read recent accessible links through the existing service cursor boundary.
- Add a JSON streaming endpoint protected by `short_links.read`.
- Exclude idempotency keys, internal identities, shares, audit data, and secrets.
- Add application/API tests and consumer documentation.

Out:

- CSV generation, archive files, background jobs, unbounded exports, tenant
  partitioning, and retention workers.

#### Acceptance Criteria

- `GET /api/short-links/export` requires `short_links.read` and returns only
  links accessible to the current actor.
- Export records are emitted through an `IAsyncEnumerable` boundary in stable
  newest-first order.
- The requested limit is clamped to a documented maximum and reads are paged
  through provider-neutral service contracts.
- Records contain only code, original URL, creation/expiry timestamps, active
  state, and access level; idempotency keys and creator identity fields are not
  serialized.
- API and application coverage verifies ordering, bounds, permissions, access
  scope, and safe fields.

#### Foundation for Next Step

Import and export now share bounded integration contracts. Later work can add
streaming transports, file formats, background execution, tenant partitioning,
or retention without changing safe record semantics.

#### Affected Files

- `src/ShortenLink.Application/Contracts/Responses/`
- `src/ShortenLink.Application/Features/ShortLinks/Export/`
- `src/ShortenLink.Api/Endpoints/ShortLinkManagementEndpoints.cs`
- `shared/ShortenLink.Hosting/ShortenLinkEndpointMappings.cs`
- `tests/ShortenLink.Application.Tests/`
- `tests/ShortenLink.Api.Tests/`
- `README.md`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Focused export ordering, bounds, permission, access-scope, and safe-field tests.

#### Done Notes

Done. Added a bounded `IAsyncEnumerable` export query over the existing
provider-neutral recent-link cursor boundary, mapped `GET
/api/short-links/export` in both API and reusable hosting profiles, enforced
`short_links.read` plus actor access scope, and serialized only safe link and
access fields. Documented the 100-record default and 1,000-record maximum.
Verification passed with a warning-free solution build, 7 focused export tests,
and all 197 solution tests.

### 027_003 - Streaming bulk import execution and per-item persistence results

#### Step Goal

Execute a bounded import item stream one item at a time, reusing the durable
create/idempotency boundary and returning stable success, replay, conflict, and
failure results for partial batches.

#### Dependency

- `027_001` durable idempotency and replay/error boundary.
- `027_002` async-enumerable-compatible validation and per-item error contract.

#### Scope

In:

- Expose a streaming-compatible import validation sequence for execution.
- Persist valid items through `IShortLinkService.CreateAsync` with the current
  actor attribution and replay-safe audit behavior.
- Add an import execution endpoint and result totals with per-item short-code,
  replay, conflict, and failure fields.
- Keep one item failure from aborting the remaining bounded batch.
- Add API/application tests and consumer documentation.

Out:

- Background queues, unbounded uploads, bulk export, tenant partitioning, and
  retention workers.

#### Acceptance Criteria

- The execution endpoint requires `short_links.import` and processes items in
  input order through an async-enumerable boundary.
- Valid items are persisted immediately through the existing create service;
  keyed replays return the existing code without duplicate audit events.
- Invalid items, idempotency conflicts, and persistence failures are reported
  per item while later items continue processing.
- Results do not echo original URLs, idempotency keys, or other input secrets.
- The documented batch bound is enforced and the response reports truncation
  and deterministic success/failure/replay totals.

#### Foundation for Next Step

Bulk import now has a retry-safe execution boundary. Future work can add
streaming transports, export, background workers, or tenant-aware partitioning
without changing per-item semantics.

#### Affected Files

- `src/ShortenLink.Core/Contracts/Results/ShortLinkImportResults.cs`
- `src/ShortenLink.Application/Abstractions/IShortLinkImportValidator.cs`
- `src/ShortenLink.Application/Services/ShortLinkImportValidator.cs`
- `src/ShortenLink.Application/Features/ShortLinks/Import/`
- `shared/ShortenLink.Hosting/ShortenLinkEndpointMappings.cs`
- `src/ShortenLink.Api/Endpoints/ShortLinkManagementEndpoints.cs`
- `tests/ShortenLink.Application.Tests/`
- `tests/ShortenLink.Api.Tests/`
- `README.md`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Focused streaming, replay, conflict, partial-failure, audit, and permission tests.

#### Done Notes

Done. Added async-enumerable-compatible execution over the existing import
validation boundary, persisted valid items through the idempotent create
service, isolated validation/conflict/persistence failures per item, and
suppressed duplicate audit events for replays. Added `POST /api/short-links/import`
with deterministic success/failure/replay totals, short-code results, and the
documented batch bound. Verification passed with a warning-free solution build
and 191 tests.

### 027_001 - Idempotent create request contract and persistence key boundary

#### Step Goal

Allow callers to retry a create request with an optional `Idempotency-Key`
without creating a second short link, while preserving the current random-code
path when no key is supplied.

#### Dependency

- Phase 026 event and observability contracts.
- Existing provider-neutral `IShortLinkRepository` and create service boundary.

#### Scope

In:

- Add an optional idempotency key to the core create request and HTTP contract.
- Persist a unique provider-neutral idempotency key for created links.
- Return the original link for an equivalent replay and reject key reuse with a
  different request or actor.
- Keep replayed creates from writing duplicate mutation audit events.
- Add SQLite/provider contract coverage and consumer documentation.

Out:

- Bulk import/export, tenant partitioning, expiration workers, and retention.
- Idempotency for update, delete, authentication, or non-create operations.

#### Acceptance Criteria

- A create request without `Idempotency-Key` behaves exactly as before.
- The first keyed request creates one link; an equivalent replay returns the
  same link and does not add another persisted link or audit event.
- Concurrent keyed requests converge on one link through a unique persistence
  boundary rather than an application-only lock.
- Reusing a key with a different URL, expiry, or actor returns a stable conflict
  error without exposing stored request data.
- The key is not returned in API responses or diagnostic/event payloads.
- Custom stores can adopt the provider-neutral idempotency lookup contract.

#### Foundation for Next Step

Create operations now have a durable replay/error boundary that bulk import,
streaming validation, and per-item failure reporting can reuse.

#### Affected Files

- `src/ShortenLink.Core/Contracts/Requests/`
- `src/ShortenLink.Core/Contracts/Results/`
- `src/ShortenLink.Core/Abstractions/`
- `src/ShortenLink.Core/Domain/ShortLinkEntity.cs`
- `src/ShortenLink.Application/Features/ShortLinks/Create/`
- `src/ShortenLink.Application/Services/ShortLinkService.cs`
- `shared/ShortenLink.Hosting/ShortenLinkEndpointMappings.cs`
- `src/ShortenLink.Api/Endpoints/ShortLinkManagementEndpoints.cs`
- `src/ShortenLink.Infrastructure/Repositories/`
- `src/ShortenLink.Infrastructure/Persistence/`
- `tests/ShortenLink.Core.Tests/`
- `tests/ShortenLink.Infrastructure.Tests/`
- `tests/ShortenLink.Api.Tests/`
- `README.md`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Focused replay, conflict, concurrency, schema, and audit-count tests.

#### Done Notes

Done. Added optional `Idempotency-Key` create support, a provider-neutral lookup
contract, unique SQLite/PostgreSQL persistence boundaries, equivalent replay
handling, conflict validation, and audit suppression for replays. API replays
return the original link with HTTP 200 while unkeyed creates retain HTTP 201.
Verification passed with a solution build and 187 tests.

### 027_002 - Bulk import dry-run contract and per-item failure boundary

#### Step Goal

Define a bounded, streaming-compatible dry-run validation boundary for bulk
short-link imports that reports stable per-item errors without writing links,
audit events, or secrets.

#### Dependency

- `027_001` durable idempotency and replay/error boundary.
- Existing URL, expiry, permission, and request validation contracts.

#### Scope

In:

- Import item/request/result contracts with per-item ordinal and error fields.
- Async-enumerable validation with a bounded batch limit and duplicate-key
  detection.
- Admin import permission and a dry-run endpoint with no persistence side
  effects.
- Tests and documentation for the future streaming importer boundary.

Out:

- Persisting import items, bulk export, background workers, tenant partitioning,
  and retention.

#### Acceptance Criteria

- Dry-run accepts a batch of import items and returns deterministic totals plus
  one result per processed item.
- Invalid URL, expiry, oversized key, and duplicate in-batch key errors use
  stable codes/messages without echoing input data.
- Dry-run performs no repository, cache, audit, or event writes.
- Input processing is async-enumerable compatible and bounded by a documented
  maximum item count.
- The endpoint requires the existing `short_links.import` permission.

#### Foundation for Next Step

Bulk import has a reusable validation/error stream that a future persistence
worker can consume item-by-item while preserving dry-run behavior.

#### Affected Files

- `src/ShortenLink.Core/Contracts/Requests/`
- `src/ShortenLink.Core/Contracts/Results/`
- `src/ShortenLink.Application/Features/ShortLinks/Import/`
- `src/ShortenLink.Application/Services/ShortLinkImportValidator.cs`
- `shared/ShortenLink.Hosting/ShortenLinkEndpointMappings.cs`
- `src/ShortenLink.Api/Endpoints/ShortLinkManagementEndpoints.cs`
- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `tests/ShortenLink.Application.Tests/`
- `tests/ShortenLink.Api.Tests/`
- `README.md`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
- Focused dry-run, bounded-stream, permission, and no-side-effect tests.

#### Done Notes

Done. Added bounded async-enumerable-compatible import contracts, deterministic
per-item validation results, duplicate-key detection, import permission wiring,
and `POST /api/short-links/import/dry-run` with no persistence or audit side
effects. Errors never echo input URLs or keys. Verification passed with a
warning-free solution build and 190 tests.

## Next Phase Proposal

Phase 027 is complete. The next phase can focus on the planned backend
refactor, followed by a separate frontend refactor phase, without carrying
expiration scheduling or destructive cleanup into the current boundary.

## Scan Rule

Keep retries and integration boundaries inside Phase 027. Do not start Phase 028
until the Phase 027 done criteria are verified.
