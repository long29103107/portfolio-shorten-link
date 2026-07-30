---
phase: 021
title: Durable Audit Trail and Investigation
status: complete
created_at: 2026-07-25
updated_at: 2026-07-29
current_task: null
task_count: 3
done_count: 3
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
| 021_002 | Authentication and security administration audit producers | done | 2026-07-28T15:33:11+07:00 |
| 021_003 | Audit investigation UI and phase closure | done | 2026-07-29T10:43:30+07:00 |

## Current Task

No active task. Phase 021 is complete.

## Completed Notes

- `021_001` added durable short-link audit persistence, deterministic
  newest-first discovery, permission-aware Admin/User scoping, action/target/
  actor/time filters, and non-secret actor/target context.
- Create, update, activate, deactivate, delete, share grant/update, and share
  revoke handlers now append exactly one successful audit event.
- `021_002` generalized typed targets and added successful authentication,
  user-owned API-key, user, role, permission-override, and persisted
  security-assignment producers without storing credentials or hashes.
- User audit visibility now includes only that user's authentication and API-key
  events in addition to existing short-link access; security-administration
  events remain Admin-only.
- Verification passed with a zero-warning backend build and 164 backend tests:
  46 Core, 5 Application, 38 Infrastructure, and 75 API.
- `021_003` added the permission-gated `/audit-logs` investigation workspace,
  exact backend filters, opaque cursor pagination, responsive event
  presentation, and explicit loading, empty, recovery, forbidden, and
  end-of-results behavior.
- Final closure verification passed with 46 frontend tests, a production
  frontend build, a zero-warning backend build, and all 164 backend tests.
- Post-closure reliability hardening moved audit delivery to a post-commit,
  fail-open queue and separate persistence worker. Audit storage failures are
  logged and do not roll back successful business operations; discovery is
  eventually consistent.

## Next Task Proposal

Phase 021 is complete. Select the next product priority and create Phase 022
only when explicitly requested.

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

### 021_002 - Authentication and Security Administration Audit Producers

#### Step Goal

Extend the durable audit boundary established by `021_001` so successful
authentication, user-owned API-key, and security-administration mutations emit
permission-aware, non-secret events without redesigning persistence or the
existing audit query endpoint.

#### Dependency

- `021_001` established the durable audit entity, repository, writer, scoped
  newest-first query, and successful short-link mutation producers.
- Authentication and security-administration use cases already execute through
  Application Mediator command handlers with injectable request context and
  time seams.
- Admin/User audit visibility already uses the event owner and current access
  scope; this task must preserve that boundary for identity targets.

#### Scope

In:

- Generalize the audit event target contract beyond short links with stable
  action and target-type values for authentication, users, roles, persisted
  security assignments, and user-owned API keys.
- Record exactly one event after each successful login and refresh operation,
  using the authenticated user identity without persisting passwords, access
  tokens, refresh tokens, credential hashes, or raw API keys.
- Record exactly one event for successful current-user API-key create, rename,
  and disable operations, scoped to the owning user.
- Record exactly one event for successful admin user create/update/disable,
  custom-role create/update/delete, role-permission override replacement, and
  persisted security-assignment create/update/disable operations.
- Preserve Admin system-wide audit visibility while allowing a User to inspect
  only their own authentication and API-key events.
- Add focused Core, Application, Infrastructure, and API tests for event
  shape, producer coverage, authorization scope, and secret exclusion.

Out:

- Failed-login or denied-operation audit events.
- Logout/session-revocation behavior that does not currently exist.
- Frontend audit-log pages, dashboard visualization, retention, export,
  archival, or external log shipping.
- Recording request bodies, passwords, tokens, raw API keys, credential keys,
  credential hashes, or other secret material.
- Broad changes to security endpoint routes or response contracts.

#### Acceptance Criteria

- Audit actions and target types are stable, explicit, and cover every
  successful mutation listed in scope.
- Successful login and refresh events identify the resulting user and contain
  no password or token material.
- Successful user-owned API-key events identify the API-key id and owner but
  contain neither the raw key nor its hash.
- Successful security-administration events identify the authenticated actor,
  target kind, stable target id, action, outcome, and occurrence time.
- Each successful command appends exactly one event; validation,
  authentication, authorization, not-found, conflict, and other failed
  commands do not append a success event.
- Admin can query all matching identity/security events; User results include
  only that User's authentication and API-key events and do not expose other
  users or global administration activity.
- Existing short-link audit producers, filtering, pagination, and visibility
  remain unchanged.
- Focused persistence, handler, and endpoint tests pass together with the
  relevant backend build.

#### Foundation for Next Step

Leaves complete backend producer coverage and a permission-aware audit query so
the next task can add an investigation UI, verify cross-layer behavior, and
close Phase 021 without revisiting audit persistence or mutation handlers.

#### Affected Files

Expected starting points:

- `.okf/phase/021/PHASE_SUMMARY.md`
- `src/ShortenLink.Core/Domain/ShortLinkAuditEvent.cs`
- `src/ShortenLink.Core/Contracts/Queries/ShortLinkAuditQuery.cs`
- `src/ShortenLink.Application/Features/Audit/`
- `src/ShortenLink.Application/Features/Security/Sessions/`
- `src/ShortenLink.Application/Features/Security/ApiKeys/`
- `src/ShortenLink.Application/Features/Security/Users/`
- `src/ShortenLink.Application/Features/Security/Roles/`
- `src/ShortenLink.Application/Features/Security/Assignments/`
- `src/ShortenLink.Infrastructure/Repositories/EfCoreShortLinkAuditRepository.cs`
- `tests/ShortenLink.Core.Tests/`
- `tests/ShortenLink.Application.Tests/`
- `tests/ShortenLink.Infrastructure.Tests/`
- `tests/ShortenLink.Api.Tests/`

#### Verification

Run after implementation:

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal
```

Also add focused assertions that persisted event fields and serialized API
responses do not contain submitted passwords, access/refresh tokens, raw API
keys, credential keys, or their hashes.

#### Done Notes

- Added stable actions and target types for authentication, user-owned API keys,
  security users, roles, permission overrides, and persisted security
  assignments while preserving the existing short-link event contract.
- Successful login and refresh append one user-owned authentication event.
  Successful API-key create, rename, and disable append one owner-scoped event
  keyed by the public API-key id.
- Successful admin user create/update/disable, custom-role
  create/update/delete, permission-override replacement, and assignment
  create/update/disable append one Admin-visible event.
- Assignment audit targets use the durable technical Guid; submitted credential
  keys and their hashes are never used as event targets or details.
- User visibility includes only owned authentication/API-key events plus the
  existing owned/shared short-link scope. Shared short codes cannot broaden
  access to non-short-link targets.
- Documented the extended producer and visibility contract in `README.md`.
- Verification:
  - `dotnet build ShortenLink.slnx --no-restore --verbosity minimal` passed with
    0 warnings and 0 errors.
  - `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
    passed: 46 Core, 5 Application, 38 Infrastructure, and 75 API tests.
  - Focused API assertions verify failed login does not create a success event
    and serialized audit results exclude submitted passwords, access/refresh
    tokens, raw API keys, credential keys, and credential hashes.

### 021_003 - Audit Investigation UI and Phase Closure

#### Step Goal

Turn the verified permission-aware audit query into a compact investigation
workspace that both Admin and User can reach according to `audit_logs.read`,
then verify the complete cross-layer audit flow and close Phase 021.

#### Dependency

- `021_001` established durable audit persistence, stable newest-first cursor
  pagination, filters, and the permission-aware `GET /api/audit-logs` contract.
- `021_002` completed successful short-link, authentication, API-key, user,
  role, permission-override, and security-assignment producer coverage.
- Admin/User scope and secret exclusion are enforced by the backend; the
  frontend must consume that contract without recreating or broadening it.

#### Scope

In:

- Add frontend audit event/page/query types and one API client for
  `GET /api/audit-logs`.
- Add an authenticated `/audit-logs` route and workspace navigation entry for
  callers whose current session includes `audit_logs.read`.
- Build a compact, responsive investigation page showing occurrence time,
  actor, action, target type/id, outcome, and safe optional subject/detail.
- Support action, target-id, actor-id, and from/to investigation filters using
  the existing backend query contract.
- Load the newest page first and allow the user to load older pages through the
  opaque `nextCursor` without duplicates or reordered results.
- Provide explicit loading, empty, forbidden, retryable-failure, and
  end-of-results states using the existing shared API failure/recovery behavior.
- Add focused routing, query-building, pagination/filter-state, presentation,
  and recovery tests.
- Update user-facing documentation, run full backend/frontend verification, and
  close Phase 021 only when every phase done criterion is evidenced.

Out:

- New audit persistence, producer, authorization, or API endpoint design.
- Client-side reconstruction of Admin/User scope or filtering of unauthorized
  server results.
- Failed-attempt auditing, logout/session-revocation producers, live polling,
  streaming, or push updates.
- Retention, archival, export, external log shipping, compliance reporting, or
  a dedicated event-detail drill-down.
- Dashboard metrics or aggregation derived from audit events.

#### Acceptance Criteria

- `/audit-logs` resolves through the app router and is reachable from workspace
  navigation only when the current user has `audit_logs.read`.
- Direct navigation remains authenticated and uses the existing stable
  unauthorized/forbidden recovery behavior.
- The first request is newest-first and each subsequent request forwards the
  opaque cursor returned by the previous page.
- Loading older events appends without duplicates, preserves server order, and
  exposes a clear end-of-results state.
- Action, target-id, actor-id, and valid from/to filters are encoded exactly as
  the backend contract expects; applying or clearing filters resets pagination.
- Each row/card clearly identifies when, who, what action, target kind/id, and
  outcome, while optional subject/detail values are shown only when present.
- Admin receives the system-wide results returned by the API. User receives
  only their backend-scoped short-link, authentication, and API-key results;
  the UI does not attempt to broaden either scope.
- Loading, empty, retryable failure, forbidden, and successful populated states
  are usable at desktop and narrow viewport widths.
- Focused frontend tests pass, the production frontend build passes, all
  backend tests remain green, and the Phase 021 summary records closure
  evidence without exposing secret values in fixtures or documentation.

#### Foundation for Next Step

Leaves Phase 021 complete with durable producer coverage, permission-aware
discovery, and a usable investigation surface so the next phase can be selected
from product priorities without revisiting the audit foundation.

#### Affected Files

Expected starting points:

- `.okf/phase/021/PHASE_SUMMARY.md`
- `README.md`
- `src/ShortenLink.Web/src/app/App.tsx`
- `src/ShortenLink.Web/src/app/router.ts`
- `src/ShortenLink.Web/src/features/short-links/types.ts`
- `src/ShortenLink.Web/src/features/short-links/api/adminSecurity.ts`
- `src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts`
- `src/ShortenLink.Web/src/features/short-links/pages/AuditLogPage.tsx`
- `src/ShortenLink.Web/src/features/short-links/auditDiscovery.ts`
- `src/ShortenLink.Web/src/styles.css`
- `src/ShortenLink.Web/test/`

#### Verification

Run after implementation:

```powershell
Set-Location .\src\ShortenLink.Web
bun test
bun run build
Set-Location ..\..
dotnet build ShortenLink.slnx --no-restore --verbosity minimal
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal
```

Also exercise the populated, empty, filtered, multi-page, retryable-failure,
unauthorized, and forbidden UI states with non-secret fixtures.

#### Done Notes

- Added an authenticated, permission-gated `/audit-logs` route and workspace
  navigation entry that consumes the backend's `audit_logs.read` scope without
  reconstructing authorization in the client.
- Added typed audit discovery with action, target-id, actor-id, and from/to
  filters, newest-first initial loading, exact opaque cursor forwarding,
  duplicate-safe older-page merging, and pagination reset on filter changes.
- Added compact desktop and narrow-width audit presentation for actor, action,
  target, occurrence time, outcome, and optional non-secret context.
- Added explicit initial loading, empty, retryable failure, forbidden, inline
  older-page failure, and end-of-results states using the shared API failure
  contract.
- Added focused routing and discovery tests and documented the investigation UI
  contract in `README.md`.
- Browser smoke verification passed for populated, filtered, empty, and 390px
  narrow-width states with no horizontal overflow or console warnings/errors.
- Verification:
  - `bun test` passed: 46 tests, 0 failures.
  - `bun run build` passed.
  - `dotnet build ShortenLink.slnx --no-restore` passed with 0 warnings and
    0 errors.
  - `dotnet test ShortenLink.slnx --no-build --no-restore` passed: 46 Core,
    5 Application, 38 Infrastructure, and 75 API tests (164 total).

## Scan Rule

Reuse the existing mediator, typed-exception, permission, actor, ownership, and
share-access contracts. Keep endpoint groups thin, never persist secrets, and do
not let audit-query filters broaden an actor's authorized visibility.
