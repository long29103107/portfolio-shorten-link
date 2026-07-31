---
phase: 027
title: Reliable Integration Workflows
status: active
created_at: 2026-07-31
updated_at: 2026-07-31
current_task: null
task_count: 3
done_count: 3
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

## Current Task

No task is active.

## Completed Notes

`027_001` completed the durable create replay boundary.

`027_002` completed bounded dry-run validation and the per-item import error
boundary.

`027_003` completed retry-safe streaming import execution with per-item
persistence results and audit-safe replays.

## Task Notes

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

## Next Task Proposal

`027_004` - Streaming bulk export read boundary.

- Expose a read-only, permission-aware export contract over recent links.
- Stream bounded records without coupling consumers to the database provider.
- Preserve safe field boundaries and avoid exporting idempotency keys or secrets.

## Scan Rule

Keep retries and integration boundaries inside Phase 027. Do not start Phase 028
until the Phase 027 done criteria are verified.
