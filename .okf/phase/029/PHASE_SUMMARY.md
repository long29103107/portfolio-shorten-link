---
phase: 029
title: Backend Performance Optimization
status: complete
created_at: 2026-08-03
updated_at: 2026-08-03
current_task: none
task_count: 5
done_count: 5
depends_on:
  - 028
---

# Phase 029 Summary

## Phase Goal

Reduce avoidable database, queue, and authorization work while preserving
existing API contracts and business behavior.

## Phase Done Criteria

- Read-heavy paths push filtering, ordering, pagination, and aggregation to the
  configured database instead of loading unbounded tables into application
  memory.
- Background workers start only when their feature is enabled and failed
  messages do not create unbounded retry loops.
- Authorization and analytics hot paths avoid avoidable N+1 queries.
- Existing API, application, infrastructure, core, and messaging verification
  remains green.

## Task Index

| Task | Title | Status | Done At |
|---|---|---|---|
| 029_001 | Push ShortLink list and expiration queries into SQL | done | 2026-08-03 |
| 029_002 | Push audit and click analytics queries into SQL | done | 2026-08-03 |
| 029_003 | Make analytics workers conditional and retries bounded | done | 2026-08-03 |
| 029_004 | Batch authorization role and permission lookups | done | 2026-08-03 |
| 029_005 | Add composite indexes for tenant and cursor query shapes | done | 2026-08-03 |

## Current Task

`029_001` is complete. The ShortLink repository now applies tenant/access,
search/status, cursor, ordering, count, and page limits through bounded EF
queries. SQLite cursor paths use parameterized SQL for DateTimeOffset boundaries
and only scan the cursor timestamp bucket for the code tie-breaker.

## Task Notes

### 029_001 - Push ShortLink list and expiration queries into SQL

#### Step Goal

Keep the existing list, access-scope, cursor, page, and expiration semantics
while making the database perform filtering, ordering, counting, and limiting.

#### Use Cases

- An admin opens the short-link list after the table has grown beyond memory
  size; only the requested page should be read.
- An export walks short links with a cursor; each batch should seek from the
  cursor instead of re-reading all earlier rows.
- The expiration executor processes a tenant batch; the database should return
  only the next `limit + 1` candidates.

#### Scope

In:

- `EfCoreShortLinkRepository` list, accessible-list, page, and expiration
  queries.
- SQL-translatable tenant/access/status/search/sort/cursor predicates.
- Repository regression tests for existing behavior.

Out:

- Audit/click query pushdown, authorization caching, schema/index changes,
  queue retry policy, and API contract changes.

#### Acceptance Criteria

- The affected repository methods no longer call `ToListAsync` before applying
  their database filters, ordering, cursor, or limit.
- Page totals use a database count query and page items use a bounded query.
- Existing repository and solution tests remain green.
- No public request, response, route, or domain behavior changes.

#### Foundation for Next Step

ShortLink reads become bounded and database-backed, leaving audit/click query
pushdown as the next isolated performance task.

#### Affected Files

- `src/ShortenLink.Infrastructure/Repositories/EfCoreShortLinkRepository.cs`
- `tests/ShortenLink.Infrastructure.Tests/EfCoreShortLinkRepositoryTests.cs`

#### Verification

- `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers`
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`

#### Done Notes

Done. Repository tests passed 45/45, and the full solution build plus test run
passed 219/219 tests.

## Next Task Proposal

Phase 029 is complete. All checklist items were implemented sequentially and
verified without changing public API contracts.

### 029_002 - Push audit and click analytics queries into SQL

#### Step Goal

Keep audit discovery and click analytics bounded by applying tenant, cursor,
time-window, ordering, count, and limit operations in the database.

#### Use Cases

- An administrator opens audit history after many mutations; only the visible
  page is materialized.
- Analytics reads click count and last-clicked time without loading every click
  timestamp into application memory.
- A tenant requests recent click activity with a safe limit and cursor.

#### Scope

In:

- `EfCoreShortLinkAuditRepository` list and action queries.
- `EfCoreShortLinkClickRepository` summary and recent-list queries.
- Regression tests for bounded result semantics.

Out:

- Worker activation/retry policy, authorization query batching, indexes, and
  public API contract changes.

#### Acceptance Criteria

- Audit and click read methods do not materialize the full table before filters,
  ordering, aggregation, or limits.
- Click summary uses database count/max operations.
- Existing repository and solution tests remain green.

#### Foundation for Next Step

Audit and analytics reads are bounded, leaving queue worker lifecycle and retry
policy isolated for the next task.

#### Affected Files

- `src/ShortenLink.Infrastructure/Repositories/EfCoreShortLinkAuditRepository.cs`
- `src/ShortenLink.Infrastructure/Repositories/EfCoreShortLinkClickRepository.cs`
- Related infrastructure tests.

#### Verification

- Focused infrastructure tests.
- Full solution build and test suite.

#### Done Notes

Done. Audit list/action queries and click summary/recent queries now apply
filters, aggregates, ordering, and limits in the database. SQLite audit time
windows use parameterized SQL boundaries because the provider cannot translate
DateTimeOffset comparisons. Infrastructure tests passed 45/45 and the full
solution verification passed 219/219.

### 029_003 - Make analytics workers conditional and retries bounded

#### Step Goal

Only register analytics/audit background consumers when their feature and queue
mode require them, and prevent transient failures from creating infinite hot
requeue loops.

#### Use Cases

- A deployment with analytics disabled should not open a RabbitMQ connection or
  start an idle click worker.
- Synchronous analytics mode should not also register an asynchronous worker.
- A poison audit/click message should be rejected or dead-lettered instead of
  being requeued forever at full speed.

#### Scope

In:

- `ShortenLinkServiceCollectionExtensions` worker registration conditions.
- Audit/click background consumer failure acknowledgement policy.
- Configuration and messaging regression tests.

Out:

- Authorization batching, schema/index changes, and API contract changes.

#### Acceptance Criteria

- Disabled or synchronous analytics does not register its queue/worker.
- Failed deliveries have bounded retry behavior and do not hot-loop forever.
- Existing enabled memory/RabbitMQ flows retain their contracts.
- Full solution verification remains green.

#### Foundation for Next Step

Queue lifecycle and failure behavior become explicit, leaving authorization
query batching as the next isolated backend optimization.

#### Affected Files

- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `shared/ShortenLink.Hosting/ShortLinkAuditBackgroundService.cs`
- `shared/ShortenLink.Hosting/ShortLinkClickBackgroundService.cs`
- Related hosting/messaging tests.

#### Verification

- Focused hosting and messaging tests.
- Full solution build and test suite.

#### Done Notes

Done. Analytics worker activation now resolves the runtime options before
constructing the click consumer, so disabled/synchronous deployments do not
open the click queue. Audit and click consumers reject failed deliveries
without requeueing, preventing poison-message hot loops while allowing durable
RabbitMQ dead-letter configuration to handle rejected messages. Full solution
verification passed 219/219 tests.

### 029_004 - Batch authorization role and permission lookups

#### Step Goal

Remove avoidable N+1 role/permission repository calls from user-session and
authorization evaluation paths while preserving effective permissions.

#### Use Cases

- A signed-in admin makes a request without querying each assigned role in a
  separate round trip.
- A user with several custom roles gets one batched role/override lookup per
  authorization decision.
- Session bootstrap and refresh remain fast as role assignments grow.

#### Scope

In:

- `ShortenLinkUserSessionService` role/permission loading.
- `ShortenLinkAuthorizationService` role/permission loading.
- Repository contracts and regression tests needed for batched reads.

Out:

- Schema/index changes, API contract changes, and frontend behavior.

#### Acceptance Criteria

- Role and permission data are fetched in batch instead of one repository call
  per role/override.
- Effective permission and denial behavior remains unchanged.
- Focused security tests and full solution verification remain green.

#### Foundation for Next Step

Authorization hot paths have bounded repository round trips, leaving composite
indexes for the final database-shape optimization task.

#### Affected Files

- `shared/ShortenLink.Hosting/ShortenLinkUserSessionService.cs`
- `shared/ShortenLink.Hosting/ShortenLinkAuthorizationService.cs`
- Related security repository contracts/tests.

#### Verification

- Focused security/application tests.
- Full solution build and test suite.

#### Done Notes

Done. Session principal and API-key authorization now batch custom-role and
permission-override reads once per decision instead of querying once per role.
Full solution verification passed 219/219 tests.

### 029_005 - Add composite indexes for tenant and cursor query shapes

#### Step Goal

Align persistence indexes with the optimized tenant-scoped list, expiration,
audit, and click query predicates without changing domain behavior.

#### Use Cases

- Tenant short-link lists seek by tenant and creation/expiration cursor.
- Expiration workers scan one tenant's next candidates without a broad index
  scan.
- Tenant analytics and audit reads narrow by tenant/code/time efficiently.

#### Scope

In:

- EF Core model indexes for short links, clicks, and audit events.
- SQLite/PostgreSQL schema/index regression assertions.

Out:

- New query features, API contracts, and frontend changes.

#### Acceptance Criteria

- Composite indexes cover the hot tenant/cursor predicates and preserve the
  existing unique constraints.
- Both configured database providers expose the expected index metadata.
- Full solution verification remains green.

#### Foundation for Next Step

The backend optimization checklist is complete and ready for phase closure
review.

#### Affected Files

- `src/ShortenLink.Infrastructure/Persistence/ShortLinkDbContext.cs`
- Related infrastructure schema tests.

#### Verification

- Infrastructure schema/index tests.
- Full solution build and test suite.

#### Done Notes

Done. Added tenant/cursor/code indexes for short links, tenant/code/time
indexes for clicks, and owner/target/time indexes for audit queries. SQLite and
PostgreSQL model/index verification plus the full solution suite passed.

## Phase Completion Notes

Phase 029 completed all backend optimization checklist items:

- `029_001`: ShortLink list/expiration/page queries use bounded database work.
- `029_002`: Audit and click list/aggregate queries use database filters,
  ordering, limits, count, and max semantics.
- `029_003`: Analytics workers activate from runtime options and failed queue
  deliveries are rejected without infinite requeue loops.
- `029_004`: Authorization role and override reads are batched per decision.
- `029_005`: Composite indexes align the hot tenant/cursor query shapes.

The full solution build passed with zero errors, and the full test suite passed
`219/219` tests.
