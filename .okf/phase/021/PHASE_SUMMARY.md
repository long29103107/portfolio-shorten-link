---
phase: 021
title: Durable Audit Trail and Investigation
status: active
created_at: 2026-07-25
updated_at: 2026-07-25
current_task: null
task_count: 1
done_count: 1
depends_on:
  - 020
---

# Phase 021 Summary

## Phase Goal

Deliver a durable, permission-aware audit trail for short-link, sharing,
authentication, and security-administration mutations so authorized users can
investigate who changed what and when without relying on current-state records.

## Phase Done Criteria

- Audit events are persisted independently from the records they describe.
- Events identify the actor, action, target, outcome, and occurrence time without
  storing credentials or other sensitive secret material.
- Short-link ownership, lifecycle, sharing, authentication, and
  security-administration mutations emit appropriate audit events.
- Audit queries enforce `audit_logs.read` and preserve the Admin/User boundary:
  Admin can inspect system-wide events, while User visibility remains scoped to
  events they are allowed to inspect.
- Query results support stable newest-first pagination and useful investigation
  filters.
- Audit behavior has focused persistence, application, and endpoint tests, and
  relevant builds pass.

## Task Index

| Task | Title | Status | Done At |
|---|---|---|---|
| 021_001 | Persisted short-link mutation audit and scoped query API | done | 2026-07-25T19:55:34+07:00 |

## Current Task

No active task. `021_001` is complete and verified; Phase 021 remains active.

## Completed Notes

- `021_001` added durable short-link audit persistence, deterministic
  newest-first discovery, permission-aware Admin/User scoping, action/target/
  actor/time filters, and non-secret actor/target context.
- Create, update, activate, deactivate, delete, share grant/update, and share
  revoke handlers now append exactly one successful audit event.
- Verification passed with a zero-warning backend build and 160 backend tests:
  45 Core, 4 Application, 37 Infrastructure, and 74 API.

## Next Task Proposal

Propose `021_002 - Authentication and security administration audit producers`
using the audit schema and append boundary verified by `021_001`. Do not create
it until requested.

## Task Notes

### 021_001 - Persisted Short-Link Mutation Audit and Scoped Query API

#### Step Goal

Establish the durable audit foundation by persisting successful short-link
mutations and exposing a permission-aware, newest-first query API that later
audit producers and investigation UI can reuse.

#### Dependency

- Phase 019 established an operational dashboard and explicitly distinguished
  its current-record activity snapshot from a durable mutation audit log.
- The Application layer now owns vertical use cases, handlers throw typed Core
  exceptions, and API endpoint groups dispatch through `ISender`.
- `audit_logs.read` already exists in the shared permission catalog and is
  granted to both system roles.
- Existing authorization preserves Admin bypass and User owner/share scoping.

#### Scope

In:

- Define a durable audit-event entity and stable contracts for actor, action,
  target, outcome, occurrence time, and non-secret contextual metadata.
- Add EF Core mapping and repository support for append and newest-first,
  paginated queries.
- Record successful short-link create, update, status-change, delete, and
  share-change mutations at the Application use-case boundary.
- Add an Application query and thin API endpoint for audit discovery.
- Require `audit_logs.read`; allow Admin system-wide visibility and scope User
  results to audit events for links they own or can access.
- Support the smallest useful filters, including action, target, actor, and time
  range, where they can be implemented without weakening authorization.
- Add focused Core/Infrastructure/Application/API tests.

Out:

- Authentication and security-administration audit producers.
- A frontend audit-log page or dashboard visualization.
- External log shipping, retention policies, archival, or compliance exports.
- Recording request bodies, passwords, API keys, credential hashes, session
  tokens, or other secrets.
- Failed-attempt auditing unless the established transaction boundary can
  represent it accurately without coupling HTTP concerns into Application.

#### Acceptance Criteria

- Audit events survive deletion or later mutation of the target short link.
- Successful create, update, activate/deactivate, delete, share grant/update,
  and share revoke operations append exactly one appropriate event.
- Event data identifies the authenticated actor and stable target identity and
  contains no secret credential material.
- The audit query is newest-first with deterministic pagination.
- Missing authentication returns the existing stable unauthorized response;
  missing `audit_logs.read` returns the existing stable forbidden response.
- Admin can query all matching events.
- User results do not expose events for inaccessible links; owner/share
  visibility follows the existing authorization contract.
- Persistence, handler, and endpoint tests cover event creation, filtering,
  pagination, authorization, and scope.
- Relevant backend build and tests pass.

#### Foundation for Next Step

Leaves one reusable audit schema, append boundary, and scoped query contract so
the next task can add authentication/security-administration producers or an
investigation UI without redesigning persistence or authorization.

#### Affected Files

Expected starting points:

- `.okf/phase/021/PHASE_SUMMARY.md`
- `src/ShortenLink.Core/Domain/`
- `src/ShortenLink.Core/Contracts/`
- `src/ShortenLink.Application/Abstractions/`
- `src/ShortenLink.Application/Features/ShortLinks/`
- `src/ShortenLink.Application/Features/Audit/`
- `src/ShortenLink.Infrastructure/Persistence/ShortLinkDbContext.cs`
- `src/ShortenLink.Infrastructure/Repositories/`
- `src/ShortenLink.Api/Endpoints/`
- `tests/ShortenLink.Core.Tests/`
- `tests/ShortenLink.Application.Tests/`
- `tests/ShortenLink.Infrastructure.Tests/`
- `tests/ShortenLink.Api.Tests/`

#### Verification

Run after implementation:

```powershell
dotnet build ShortenLink.slnx --verbosity minimal
dotnet test ShortenLink.slnx --verbosity minimal
```

For the persistence slice, also verify clean SQLite schema creation through the
smallest relevant Infrastructure or API test.

#### Done Notes

- Added `ShortLinkAuditEvent`, stable action/outcome contracts, a Guid-backed EF
  persistence entity, indexes, and `IShortLinkAuditRepository`.
- Added `ShortLinkAuditWriter` at the Application mutation boundary.
- Recorded one successful event for create, update, activate, deactivate,
  delete, share grant/update, and share revoke.
- Added `GET /api/audit-logs` with `audit_logs.read`, deterministic cursor
  pagination, action/target/actor/time filters, Admin global visibility, and
  owner/current-share User scoping.
- Persisted owner identity keeps owner audit history discoverable after target
  deletion; raw URLs and credential/session secrets are not recorded.
- Added stable non-secret actor identities for sessions/users, persisted
  assignments, configured API keys, and security-disabled local operation.
- Documented the endpoint and authorization contract in `README.md`.
- Verification:
  - `dotnet build ShortenLink.slnx --no-restore --verbosity minimal` passed with
    0 warnings and 0 errors.
  - `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
    passed: 45 Core, 4 Application, 37 Infrastructure, and 74 API tests.
  - Clean in-memory SQLite schema verification includes
    `short_link_audit_events` with a Guid `Id` primary key.

## Scan Rule

Reuse the existing mediator, typed-exception, permission, actor, ownership, and
share-access contracts. Keep endpoint groups thin, never persist secrets, and do
not let audit-query filters broaden an actor's authorized visibility.
