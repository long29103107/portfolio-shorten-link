---
phase: 031
title: Frontend Source Optimization
status: complete
created_at: 2026-08-07
updated_at: 2026-08-09
current_task: null
task_count: 23
done_count: 23
depends_on:
  - 030
---

# Phase 031 Summary

## Phase Goal

Optimize the React/Vite frontend using feature-based boundaries, smaller page
responsibilities, centralized query/API state, and measured loading behavior
without changing routes, API contracts, or user-facing behavior.

## Phase Done Criteria

- Feature imports follow a shared -> features -> app dependency direction.
- Large pages delegate data/state orchestration to focused hooks/components.
- Query/API behavior remains centralized, typed, cancellable, and compatible.
- Initial and route-level loading behavior is measured and improved without
  premature memoization or a new state library.
- Frontend tests and production build remain green after every task.

## Task Index

| Task | Title | Category | Status | Done At |
|---|---|---|---|---|
| 031_001 | Enforce frontend dependency boundaries | Architecture | done | 2026-08-07 |
| 031_002 | Decompose ShortLinkAdminPage state and data loading | Refactor | done | 2026-08-07 |
| 031_003 | Extract SecurityManagementPage data loading boundary | Refactor | done | 2026-08-07 |
| 031_004 | Centralize typed query/API state and cancellation | Refactor | done | 2026-08-07 |
| 031_005 | Add measured route loading and frontend performance checks | Performance | done | 2026-08-07 |
| 031_006 | Add typed feature API client abstraction | Refactor | done | 2026-08-07 |
| 031_007 | Centralize frontend contract constants | Refactor | done | 2026-08-07 |
| 031_008 | Extract AdminDashboardPage data loading boundary | Refactor | done | 2026-08-07 |
| 031_009 | Extract AuditLogPage data loading boundary | Refactor | done | 2026-08-08 |
| 031_010 | Add cancellation to security management discovery reads | Refactor | done | 2026-08-08 |
| 031_011 | Add cancellation to dashboard auxiliary reads | Refactor | done | 2026-08-08 |
| 031_012 | Add cancellable short-link detail read boundary | Refactor | done | 2026-08-08 |
| 031_013 | Add cancellation to the admin analytics read | Refactor | done | 2026-08-08 |
| 031_014 | Add cancellation to the admin export traversal | Refactor | done | 2026-08-08 |
| 031_015 | Add cancellation to security assignment discovery | Refactor | done | 2026-08-08 |
| 031_016 | Add automated frontend bundle/performance checks | Performance | done | 2026-08-08 |
| 031_017 | Correct dashboard recent-link pagination parameters | Correctness | done | 2026-08-08 |
| 031_018 | Suppress user-facing failure toasts for expected request cancellation | Correctness | done | 2026-08-08 |
| 031_019 | Protect imperative short-link discovery loads from stale responses | Correctness | done | 2026-08-08 |
| 031_020 | Protect short-link share dialog reads from stale responses | Correctness | done | 2026-08-08 |
| 031_021 | Decompose short-link admin mutation and dialog responsibilities | Refactor | done | 2026-08-08 |
| 031_022 | Decompose security management mutation and dialog responsibilities | Refactor | done | 2026-08-08 |
| 031_023 | Extract security role-permission workspace presentation | Refactor | done | 2026-08-09 |

## Current Task

`031_023` is complete. The role-permission workspace presentation, local search,
group expansion, staged drafts, dirty-state reporting, and save confirmation
now live in a feature-scoped component while the page retains security access,
discovery, user management, and role mutation ownership. All twenty-three
tasks provide feature boundaries, lazy route loading, cancellable discovery
reads, a typed API client, centralized frontend contracts, focused data
boundaries, a repeatable bundle/performance budget check, verified pagination,
silent expected cancellation, stale-safe discovery, and focused mutation/dialog
boundaries.

## Next Task Proposal

Phase 031 is complete. Select the next phase from the product roadmap rather
than adding more frontend refactor-only tasks here.

## Task Notes

### 031_001 - Enforce frontend dependency boundaries

#### Step Goal

Add a lightweight architecture guard for shared, feature, and app import
direction without adding a new linting framework.

#### Acceptance Criteria

- Guard rejects shared -> app/features and features -> app imports.
- Guard rejects cross-feature imports when multiple features exist.
- Package script runs the guard deterministically.

#### Verification

```powershell
bun run check:architecture
```

#### Done Notes

- Added `scripts/check-architecture.mjs` for shared/features/app import rules.
- Added the `check:architecture` package script.
- Existing source passes the guard with no violations.

### 031_002 - Decompose ShortLinkAdminPage state and data loading

#### Step Goal

Move list/query loading state from the 1,156-line page into a focused feature
hook while preserving list, filter, sort, pagination, and recovery behavior.

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added `useShortLinkDiscovery` for list state, query state, pagination, and
  recovery loading.
- Kept mutation handlers and rendered behavior in the page façade.
- Frontend architecture guard passed, 63 tests passed, and production build
  completed successfully.

### 031_003 - Extract SecurityManagementPage data loading boundary

#### Step Goal

Move initial users/roles loading and recovery state from the 979-line page into
a focused hook without changing security actions or permissions.

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added `useSecurityManagementData` for initial users/roles loading and read
  recovery state.
- Kept security mutations, permissions, and dialog state in the page.
- Frontend architecture guard passed, 63 tests passed, and production build
  completed successfully.

### 031_004 - Centralize typed query/API state and cancellation

#### Step Goal

Make feature query state and request cancellation explicit at the API boundary,
preventing stale responses when discovery criteria change.

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added optional `AbortSignal` support to `listShortLinks`.
- Discovery loading now aborts obsolete requests when query/page-size state
  changes and ignores their failures.
- Frontend architecture guard passed, 63 tests passed, and production build
  completed successfully.

### 031_005 - Add measured route loading and frontend performance checks

#### Step Goal

Lazy-load heavy feature pages and add repeatable build/bundle verification
without introducing speculative memoization.

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Lazy-loaded admin, security, dashboard, audit, and detail pages with a
  visible route loading fallback.
- Reduced the initial JavaScript bundle from 330.49 kB to 235.45 kB in the
  production build, with feature chunks emitted separately.
- Frontend architecture guard passed, 63 tests passed, and production build
  completed successfully.

### 031_006 - Add typed feature API client abstraction

#### Step Goal

Centralize short-links API calls behind typed `get`, `post`, `put`, `patch`,
`delete`, and `query` methods while preserving auth refresh, error handling,
request cancellation, and query syntax.

#### Scope

- Add a feature-scoped API client and React `useApi` hook.
- Serialize JSON request bodies in one place and keep existing HTTP behavior.
- Build query strings with `URLSearchParams` exactly once, including filters,
  sort expressions, existing parameters, and repeated values.
- Migrate short-links API functions to the client methods.

#### Acceptance Criteria

- All existing short-links API calls use the typed client instead of manually
  setting HTTP methods and JSON bodies.
- `query` preserves the raw filter value after browser/API decoding and never
  produces a double-encoded `%2528` sequence.
- `AbortSignal` continues to reach discovery requests.
- The client exposes `get`, `post`, `put`, `patch`, `delete`, and `query`.

#### Foundation for Next Step

Feature pages can consume one stable API boundary while later decomposition
work focuses on UI responsibilities and broader cancellation coverage.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/api/apiClient.ts`
- `src/ShortenLink.Web/src/shared/api/apiClient.ts`
- `src/ShortenLink.Web/src/features/short-links/api/http.ts`
- `src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts`
- `src/ShortenLink.Web/test/api-client.test.ts`

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added typed `apiClient` methods and the `useApi` React hook.
- Migrated all short-links API operations to the client, including the
  cancellable discovery request.
- Added query encoding tests proving URLSearchParams decodes back to the raw
  filter and rejects double encoding.
- Architecture guard passed, 66 tests passed, and production build completed
  successfully.

### 031_008 - Extract AdminDashboardPage data loading boundary

#### Step Goal

Move dashboard API orchestration, snapshot composition, loading state, and
refresh cancellation out of `AdminDashboardPage` while preserving dashboard
metrics, degraded-source behavior, and refresh behavior.

#### Scope

- Add `useAdminDashboardData` for dashboard reads and derived snapshot state.
- Keep the page responsible for rendering and presentation only.
- Abort obsolete dashboard link reads and ignore stale request completions.
- Preserve existing API parameters, status filters, and failure messaging.

#### Acceptance Criteria

- `AdminDashboardPage` consumes a focused data hook instead of owning the
  dashboard Promise.allSettled orchestration.
- Refresh starts a new request generation and obsolete list requests cannot
  overwrite current state.
- Dashboard metrics, health badges, rate-limit activity, and refresh controls
  remain behaviorally compatible.
- Architecture guard, tests, and production build remain green.

#### Foundation for Next Step

The dashboard page is now a presentation façade; the next decomposition can
focus on audit discovery or remaining page-level UI responsibilities.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/hooks/useAdminDashboardData.ts`
- `src/ShortenLink.Web/src/features/short-links/pages/AdminDashboardPage.tsx`

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added `useAdminDashboardData` with request-version protection and
  `AbortController` cleanup.
- Moved dashboard reads, snapshot composition, rate-limit state, and refresh
  loading state out of the page.
- Preserved the existing dashboard UI, API parameters, and degraded-source
  messaging.
- Architecture guard passed, 66 tests passed, and production build completed
  successfully.

### 031_007 - Centralize frontend contract constants

#### Step Goal

Reduce repeated hardcoded routes, API paths, HTTP values, storage keys, event
names, and pagination defaults by centralizing stable contracts as typed
constants without changing behavior.

#### Scope

- Add shared constants for application routes, HTTP methods/statuses/headers,
  and auth events.
- Add feature constants for API route builders and discovery defaults.
- Migrate app routing, auth transport, API modules, and dashboard discovery to
  the constants.
- Keep user-facing copy and one-off visual values local to their components.

#### Acceptance Criteria

- API endpoint strings and path-segment encoding are defined in one feature
  route map.
- Application navigation and route parsing use shared route constants.
- HTTP status/method/header values and session event/storage keys are not
  duplicated in transport or auth code.
- Existing query/API behavior and route contracts remain unchanged.

#### Foundation for Next Step

The frontend has a stable contract vocabulary for further page decomposition,
query cancellation coverage, and automated performance checks.

#### Affected Files

- `src/ShortenLink.Web/src/shared/constants/http.ts`
- `src/ShortenLink.Web/src/shared/constants/routes.ts`
- `src/ShortenLink.Web/src/shared/constants/events.ts`
- `src/ShortenLink.Web/src/shared/api/apiFailure.ts`
- `src/ShortenLink.Web/src/shared/api/apiClient.ts`
- `src/ShortenLink.Web/src/app/App.tsx`
- `src/ShortenLink.Web/src/app/router.ts`
- `src/ShortenLink.Web/src/features/short-links/constants/apiRoutes.ts`
- `src/ShortenLink.Web/src/features/short-links/constants/defaults.ts`
- `src/ShortenLink.Web/src/features/short-links/api/adminSecurity.ts`
- `src/ShortenLink.Web/src/features/short-links/api/http.ts`
- `src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts`
- `src/ShortenLink.Web/src/features/short-links/components/ShortLinkShareDialog.tsx`
- `src/ShortenLink.Web/src/features/short-links/auditDiscovery.ts`
- `src/ShortenLink.Web/src/features/short-links/pages/AdminDashboardPage.tsx`

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Centralized API paths and URL-segment encoding in `SHORT_LINK_API_ROUTES`.
- Centralized navigation paths, HTTP contract values, session keys, auth
  events, and discovery limits.
- Preserved existing API URLs, route parsing, filter/sort query behavior, and
  UI copy.
- Architecture guard passed, 66 tests passed, and production build completed
  successfully.

### 031_009 - Extract AuditLogPage data loading boundary

#### Step Goal

Move audit action loading, filtered event discovery, cursor pagination, retry
state, and request cancellation out of `AuditLogPage` while preserving the
existing audit table, filter form, recovery messaging, and server query
contracts.

#### Scope

- Add `useAuditLogData` for audit actions, event pages, cursor pagination,
  recovery state, and reload state.
- Add optional `AbortSignal` support to audit API reads.
- Abort obsolete filtered/older-page requests and ignore stale completions.
- Keep filter draft and time-range validation in the page presentation layer.

#### Acceptance Criteria

- `AuditLogPage` no longer owns audit API orchestration or event loading state.
- Changing filters, retrying, loading older pages, and unmounting cannot let an
  obsolete request overwrite current audit state.
- Audit action discovery and event queries preserve their existing URLs,
  limits, cursor handling, ordering, and recovery behavior.
- Architecture guard, frontend tests, and production build remain green.

#### Foundation for Next Step

Audit discovery now has the same focused, cancellable data boundary as short
link discovery and the admin dashboard. The next task can extend cancellation
coverage to security management reads without changing page behavior.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/hooks/useAuditLogData.ts`
- `src/ShortenLink.Web/src/features/short-links/pages/AuditLogPage.tsx`
- `src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts`

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added `useAuditLogData` for action discovery, filtered event loading,
  cursor-based older pages, retry behavior, and stale-request protection.
- Added optional abort signals to audit action/event API reads.
- Preserved the audit filter form, query serialization, merge behavior, and
  recovery copy while reducing `AuditLogPage` orchestration.
- Frontend architecture guard passed, 66 tests passed, and production build
  completed successfully with the audit page remaining a lazy route chunk.

### 031_010 - Add cancellation to security management discovery reads

#### Step Goal

Make security users and roles discovery cancellable and stale-safe while
preserving permission gating, refresh/retry behavior, mutation state, and the
existing API contracts.

#### Scope

- Add optional `AbortSignal` support to security users and roles list API
  functions.
- Abort an obsolete security read when refresh starts, permissions change, or
  the page unmounts.
- Ignore stale success and failure completions so old reads cannot overwrite
  current security data or recovery state.
- Keep security mutations and page-level permission checks unchanged.

#### Acceptance Criteria

- `useSecurityManagementData` passes one request signal to both users and roles
  reads.
- A refresh or permission transition cancels the previous read generation and
  only the current generation can update users, roles, loading, or failure
  state.
- Existing permission gating, mutation handlers, recovery copy, and API URLs
  remain compatible.
- Architecture guard, frontend tests, and production build remain green.

#### Foundation for Next Step

Security management discovery now has a consistent cancellable read boundary
alongside short-link and audit discovery. The next task can extend the same
signal propagation to the dashboard's auxiliary identity and rate-limit reads.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/hooks/useSecurityManagementData.ts`
- `src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts`

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added abort-signal support to `listSecurityRoles` and `listSecurityUsers`.
- Added controller cleanup, request-version protection, and stale completion
  guards to `useSecurityManagementData`.
- Preserved permission gating, refresh/retry behavior, mutation state, API
  paths, and recovery messaging.
- Frontend architecture guard passed, 66 tests passed, and production build
  completed successfully.

### 031_011 - Add cancellation to dashboard auxiliary reads

#### Step Goal

Pass the dashboard request generation's `AbortSignal` through identity,
role, and rate-limit reads so refresh, route changes, and unmount cannot let
obsolete auxiliary results overwrite the current dashboard snapshot or
rate-limit state.

#### Scope

- Add optional `AbortSignal` support to the rate-limit activity API read.
- Reuse the existing dashboard controller for users, roles, and rate-limit
  reads.
- Ignore aborted dashboard generations before composing snapshot or activity
  state.
- Preserve degraded-source behavior, metrics, health badges, and refresh UI.

#### Acceptance Criteria

- All six dashboard reads receive the same request signal.
- Obsolete refresh generations cannot update dashboard snapshot,
  rate-limit activity, error, or loading state.
- Existing dashboard API paths, degraded-source messaging, and refresh behavior
  remain compatible.
- Architecture guard, frontend tests, and production build remain green.

#### Foundation for Next Step

Dashboard discovery now has one cancellable generation across short-link,
identity, role, and rate-limit sources. The next task can focus on a remaining
page-level read boundary, starting with short-link detail loading.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/hooks/useAdminDashboardData.ts`
- `src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts`

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added optional abort-signal support to `getRateLimitActivity`.
- Passed the dashboard controller signal to users, roles, and rate-limit
  reads, with an explicit aborted-generation guard before state composition.
- Preserved dashboard metrics, degraded-source behavior, rate-limit messaging,
  refresh behavior, and API paths.
- Frontend architecture guard passed, 66 tests passed, and production build
  completed successfully.

### 031_012 - Add cancellable short-link detail read boundary

#### Step Goal

Move short-link detail discovery out of `ShortLinkDetailPage` into a focused
read hook with request cancellation and stale-response protection while
preserving detail rendering, recovery copy, and page-local deactivation state.

#### Scope

- Add `useShortLinkDetailData` for detail loading, read error, and detail state.
- Add optional `AbortSignal` support to the short-link detail API read.
- Abort obsolete detail reads when `code` changes or the page unmounts.
- Keep deactivation mutation, mutation error, and navigation behavior in the
  page.

#### Acceptance Criteria

- `ShortLinkDetailPage` no longer owns detail read orchestration or loading
  state.
- A stale detail response or read failure cannot overwrite the current code's
  detail state.
- Existing detail API paths, error mapping, loading UI, deactivate behavior,
  and navigation remain compatible.
- Architecture guard, frontend tests, and production build remain green.

#### Foundation for Next Step

Short-link detail discovery now has the same focused, cancellable read boundary
as the other feature discovery paths. The next task can extend cancellation to
the admin page's analytics read without changing mutation behavior.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/hooks/useShortLinkDetailData.ts`
- `src/ShortenLink.Web/src/features/short-links/pages/ShortLinkDetailPage.tsx`
- `src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts`

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added `useShortLinkDetailData` with abort cleanup, request-version guards,
  loading state, and friendly read error mapping.
- Added optional abort-signal support to `getShortLinkDetails`.
- Preserved page-local deactivation mutation/error behavior and detail UI.
- Frontend architecture guard passed, 66 tests passed, and production build
  completed successfully with the detail page remaining a lazy route chunk.

### 031_013 - Add cancellation to the admin analytics read

#### Step Goal

Move analytics panel read state and lifecycle out of `ShortLinkAdminPage` into
a focused hook with cancellation and stale-response protection while
preserving analytics rendering, retry behavior, and page-local mutations.

#### Scope

- Add `useShortLinkAnalyticsData` for analytics panel code, data, loading,
  errors, retry state, open/close, and retry actions.
- Add optional `AbortSignal` support to the short-link analytics API read.
- Abort obsolete analytics requests when another link opens, the panel closes,
  or the admin page unmounts.
- Keep permission checks, analytics presentation, and short-link mutations in
  the page.

#### Acceptance Criteria

- `ShortLinkAdminPage` no longer owns analytics request orchestration or read
  state.
- Opening another link, retrying, closing the panel, and unmounting cannot let
  an obsolete analytics response update the current panel.
- Existing analytics URL, error mapping, retry behavior, and rendered metrics
  remain compatible.
- Architecture guard, frontend tests, and production build remain green.

#### Foundation for Next Step

Admin analytics now has the same focused, cancellable read boundary as detail,
audit, security, and dashboard discovery. The next task can extend signal
coverage to the admin export traversal.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/hooks/useShortLinkAnalyticsData.ts`
- `src/ShortenLink.Web/src/features/short-links/pages/ShortLinkAdminPage.tsx`
- `src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts`

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added `useShortLinkAnalyticsData` with open/close/retry lifecycle,
  `AbortController` cleanup, request-version guards, and existing error
  mapping.
- Added optional abort-signal support to `getShortLinkAnalytics`.
- Removed analytics request state/orchestration from `ShortLinkAdminPage` while
  preserving permission checks, panel UI, retry behavior, and mutations.
- Frontend architecture guard passed, 66 tests passed, and production build
  completed successfully with the admin page remaining a lazy route chunk.

### 031_014 - Add cancellation to the admin export traversal

#### Step Goal

Move admin CSV export traversal into a focused hook with one cancellable
request generation across all list pages while preserving duplicate filtering,
CSV output, recovery behavior, and discovery criteria.

#### Scope

- Add `useShortLinkExport` for export loading, failure, retry, dismiss, and
  cancellation state.
- Pass an `AbortSignal` to every paginated `listShortLinks` export request.
- Cancel an export when discovery criteria change or the admin page unmounts.
- Keep CSV serialization/download and success messaging compatible.

#### Acceptance Criteria

- `ShortLinkAdminPage` no longer owns export traversal or export recovery state.
- Every export page request shares one signal and obsolete traversal cannot
  download a CSV or publish a recovery error.
- Existing page ordering, duplicate-code filtering, CSV fields, retry/dismiss
  UI, and success message remain compatible.
- Architecture guard, frontend tests, and production build remain green.

#### Foundation for Next Step

Admin export now has a focused cancellable traversal boundary alongside the
admin analytics and discovery reads. The next task can extend cancellation to
security assignment discovery.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/hooks/useShortLinkExport.ts`
- `src/ShortenLink.Web/src/features/short-links/pages/ShortLinkAdminPage.tsx`

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added `useShortLinkExport` with paginated signal propagation, cancellation
  on criteria changes/unmount, request-version guards, and recovery state.
- Removed export traversal and export state from `ShortLinkAdminPage` while
  preserving CSV serialization, duplicate filtering, and UI behavior.
- Frontend architecture guard passed, 66 tests passed, and production build
  completed successfully with the admin page remaining a lazy route chunk.

### 031_015 - Add cancellation to security assignment discovery

#### Step Goal

Give security assignment discovery the same cancellable, stale-response-safe
read boundary as the other frontend discovery surfaces without changing the
assignment management workflow.

#### Scope

- Add optional `AbortSignal` support to the security assignment list API call.
- Extract assignment discovery loading, error, and cancellation lifecycle into
  `useSecurityAssignmentsData`.
- Keep save, disable, edit, confirmation, and local list update behavior in the
  security assignments page.

#### Acceptance Criteria

- Refreshing or unmounting the security assignments page aborts the active list
  request.
- An obsolete assignment response or read error cannot overwrite the current
  request state.
- Existing permission gating, retry/refresh UI, mutation behavior, and friendly
  error messages remain compatible.
- Architecture guard, frontend tests, and production build remain green.

#### Foundation for Next Step

All known frontend discovery reads now have focused cancellation boundaries.
The next task can make the phase's measured loading target repeatable with
automated bundle and performance checks.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts`
- `src/ShortenLink.Web/src/features/short-links/hooks/useSecurityAssignmentsData.ts`
- `src/ShortenLink.Web/src/features/short-links/pages/SecurityAssignmentsPage.tsx`

#### Verification

```powershell
bun run check:architecture
bun test
bun run build
```

#### Done Notes

- Added optional abort-signal propagation to `listSecurityAssignments`.
- Added `useSecurityAssignmentsData` with active-request cancellation, request
  generation guards, unmount cleanup, and existing friendly read-error mapping.
- Removed discovery state and orchestration from `SecurityAssignmentsPage` while
  keeping mutations, confirmation flow, refresh UI, and local list updates
  compatible.
- Follow-up organization grouped pure feature modules under
  `features/short-links/domain/`, kept the shared feature contracts in
  `types.ts`, and updated production/test imports without changing behavior.
- Frontend architecture guard passed, 66 tests passed, and production build
  completed successfully.

### 031_016 - Add automated frontend bundle/performance checks

#### Step Goal

Make the phase's measured route-loading target repeatable by checking the
production entry asset, stylesheet, total JavaScript, largest lazy chunk, and
required lazy route chunks against explicit budgets after a production build.

#### Scope

- Add a dependency-free Node/Bun script that reads the generated Vite `dist`
  output (`index.html` plus assets) and reports asset sizes.
- Fail deterministically when an entry, total, lazy-chunk, or required-route
  budget is exceeded or when a required lazy route chunk is missing.
- Add a package script that builds first and then runs the performance check.
- Keep budgets based on the current measured bundle and do not introduce a
  runtime performance library or speculative code changes.

#### Acceptance Criteria

- `npm run check:performance`/`bun run check:performance` produces a
  production build before checking the output.
- The check validates entry JavaScript, entry CSS, total JavaScript, largest
  lazy chunk, and the five lazy route chunks currently emitted by Vite.
- The check exits non-zero with an actionable message when a budget or route
  chunk requirement fails.
- Existing architecture check, frontend tests, and production build remain
  green.

#### Foundation for Next Step

Frontend bundle and route-splitting regressions are now caught by one repeatable
command. The next task can address page ownership or async correctness with a
stable performance guard in place.

#### Affected Files

- `src/ShortenLink.Web/scripts/check-performance.mjs`
- `src/ShortenLink.Web/package.json`
- `.okf/phase/031/PHASE_SUMMARY.md`

#### Verification

```powershell
cd .\src\ShortenLink.Web
bun run check:architecture
bun test
bun run check:performance
```

#### Done Notes

- Added `scripts/check-performance.mjs` to inspect the generated Vite output,
  enforce explicit entry/total/lazy-chunk budgets, and require all five lazy
  route chunks.
- Added `check:performance` to build first and then run the asset budget check.
- Verified architecture boundaries, 66 Bun tests, the production build, and
  the performance budget successfully.

### 031_017 - Correct dashboard recent-link pagination parameters

#### Step Goal

Ensure dashboard short-link summary reads request the intended result size on
the first page, while preserving status filters, descending creation order,
degraded-source behavior, and the existing API contract.

#### Scope

- Correct the dashboard status-list helper's `listShortLinks` argument order.
- Keep dashboard metrics, recent activity composition, cancellation, and
  refresh behavior unchanged.
- Add a regression test that verifies the dashboard query uses the requested
  limit and page one.

#### Acceptance Criteria

- The dashboard recent-link read sends `limit=RECENT_LINK_LIMIT` and `page=1`.
- Active and inactive summary reads send `limit=LINK_LIMIT` and `page=1`.
- Status filters and descending creation sort remain unchanged.
- The regression test fails for the old reversed argument order and passes for
  the corrected behavior.
- Architecture check, frontend tests, production build, and performance budget
  remain green.

#### Foundation for Next Step

Dashboard summary reads now have a verified pagination contract. The next task
can safely address page ownership or another concrete async correctness gap
without carrying forward this incorrect request shape.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/domain/adminDashboard.ts`
- `src/ShortenLink.Web/src/features/short-links/hooks/useAdminDashboardData.ts`
- `src/ShortenLink.Web/test/admin-dashboard.test.ts`
- `.okf/phase/031/PHASE_SUMMARY.md`

#### Verification

```powershell
cd .\src\ShortenLink.Web
bun run check:architecture
bun test
bun run check:performance
```

#### Done Notes

- Added `buildDashboardLinkDiscovery` so dashboard summary reads carry an
  explicit requested limit and page one.
- Corrected the dashboard hook to call `listShortLinks(limit, 1, ...)`,
  preserving status filters, descending creation sort, and request signals.
- Added a regression test that verifies the typed request and resulting API URL
  use `limit=6&page=1` for recent links.
- Architecture boundaries passed, 67 Bun tests passed, production build
  completed, and the performance budget passed.

### 031_018 - Suppress user-facing failure toasts for expected request cancellation

#### Step Goal

Keep expected `AbortError` cancellations silent at the shared HTTP transport
boundary so navigation, criteria changes, refreshes, and unmount cleanup do not
appear to users as timeout or network failures.

#### Scope

- Detect expected abort errors before fetch-failure classification and toast
  emission.
- Rethrow the original abort error so existing signal-aware hooks can ignore it
  and preserve their cancellation lifecycle.
- Keep real network failures, HTTP timeouts, retryability, auth redirects, and
  existing failure classification unchanged.
- Add a transport regression test that observes no toast event for an aborted
  request.

#### Acceptance Criteria

- An aborted `fetchJson` request does not call `showToast` and does not produce
  an `ApiError` timeout toast.
- The original abort error remains observable to the caller for signal-aware
  cleanup logic.
- Non-abort fetch failures continue to classify and emit their existing
  retryable network failure behavior.
- Architecture check, frontend tests, production build, and performance budget
  remain green.

#### Foundation for Next Step

Cancellation no longer pollutes user-facing recovery UI. The next task can
address another concrete async race or begin decomposing page ownership with a
clean transport-level cancellation contract.

#### Affected Files

- `src/ShortenLink.Web/src/shared/api/apiFailure.ts`
- `src/ShortenLink.Web/src/features/short-links/api/http.ts`
- `src/ShortenLink.Web/test/http-cancellation.test.ts`
- `.okf/phase/031/PHASE_SUMMARY.md`

#### Verification

```powershell
cd .\src\ShortenLink.Web
bun run check:architecture
bun test
bun run check:performance
```

#### Done Notes

- Exported the shared `isAbortError` predicate and made `fetchJson` rethrow
  expected abort errors before failure classification/toast emission.
- Preserved existing timeout/network classification for non-abort failures and
  kept abort errors observable to signal-aware hooks.
- Added a transport regression test proving an aborted request emits no toast.
- Architecture boundaries passed, 68 Bun tests passed, production build
  completed, and the performance budget passed.

### 031_019 - Protect imperative short-link discovery loads from stale responses

#### Step Goal

Make every short-link discovery command stale-response-safe, including
pagination, retry, refresh, criteria changes, and unmount cleanup, while
preserving the existing list API, recovery UI, and query behavior.

#### Scope

- Move active `AbortController` and request-generation ownership into
  `useShortLinkDiscovery`.
- Abort the previous generation whenever `loadLinks` starts a new request.
- Ignore stale success, failure, and loading completions before they update
  links, pagination, recovery, or loading state.
- Keep the public hook command simple so page callers do not pass lifecycle
  signals manually.
- Add a regression test for current, aborted, and stale request generations.

#### Acceptance Criteria

- Initial discovery, pagination, retry, refresh, criteria changes, and unmount
  all cancel or invalidate obsolete requests.
- A stale response cannot overwrite links, totals, page number, failure state,
  or loading state from a newer request.
- Existing retry page, pagination bounds, discovery query, API URL, and user
  facing recovery behavior remain compatible.
- Architecture check, frontend tests, production build, and performance budget
  remain green.

#### Foundation for Next Step

Short-link discovery now has one lifecycle owner for both Effect and imperative
loads. The next task can address another concrete async race or proceed to
page-level ownership decomposition without exposing request cancellation to
page callers.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/domain/requestLifecycle.ts`
- `src/ShortenLink.Web/src/features/short-links/hooks/useShortLinkDiscovery.ts`
- `src/ShortenLink.Web/test/admin-discovery.test.ts`
- `.okf/phase/031/PHASE_SUMMARY.md`

#### Verification

```powershell
cd .\src\ShortenLink.Web
bun run check:architecture
bun test
bun run check:performance
```

#### Done Notes

- Added `isCurrentRequestGeneration` as the pure request-generation guard for
  short-link discovery.
- Moved active controller and generation ownership into
  `useShortLinkDiscovery`; every imperative load now aborts the previous one and
  ignores stale success, failure, and loading completions.
- Removed signal lifecycle arguments from page callers while preserving list
  query, pagination, retry, refresh, recovery, and cancellation behavior.
- Architecture boundaries passed, 69 Bun tests passed, production build
  completed, and the performance budget passed.

### 031_020 - Protect short-link share dialog reads from stale responses

#### Step Goal

Make share-dialog access discovery cancellation- and stale-response-safe when
switching links, closing the dialog, or unmounting, without changing share
mutations or the rendered sharing workflow.

#### Scope

- Add optional `AbortSignal` support to `listShortLinkShares`.
- Abort the previous share read during link changes, close, and unmount
  cleanup.
- Guard success, failure, and loading completion with the current request
  generation so an API implementation that resolves after abort cannot update
  another link's dialog state.
- Keep sharing-mode updates, add/remove mutations, confirmation flow, and error
  copy unchanged.

#### Acceptance Criteria

- Share access reads receive an abort signal and are cancelled on dialog
  cleanup.
- A stale share response or failure cannot replace the current link's shares,
  mode, error, or loading state.
- Expected cancellation does not show a failure state or false toast.
- Existing share API paths, mutation behavior, and rendered workflow remain
  compatible.
- Architecture check, frontend tests, production build, and performance budget
  remain green.

#### Foundation for Next Step

Share-dialog discovery now follows the same cancellable stale-safe read contract
as the main short-link list. The next task can address another async boundary
or proceed to page ownership decomposition.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts`
- `src/ShortenLink.Web/src/features/short-links/components/ShortLinkShareDialog.tsx`
- `src/ShortenLink.Web/test/share-discovery.test.ts`
- `.okf/phase/031/PHASE_SUMMARY.md`

#### Verification

```powershell
cd .\src\ShortenLink.Web
bun run check:architecture
bun test
bun run check:performance
```

#### Done Notes

- Added optional `AbortSignal` propagation to `listShortLinkShares`.
- Added share-dialog request generation and cleanup guards so switching links,
  closing, or unmounting cannot publish stale shares, mode, error, or loading
  state.
- Preserved share mutation, confirmation, API route, and rendered behavior.
- Added signal-propagation and current-generation regression tests.
- Architecture boundaries passed, 71 Bun tests passed, production build
  completed, and the performance budget passed.

### 031_021 - Decompose short-link admin mutation and dialog responsibilities

#### Step Goal

Move short-link admin mutation orchestration and analytics dialog rendering out
of `ShortLinkAdminPage` while preserving routes, permissions, API contracts, and
the existing create/edit/status/delete/share/analytics workflow.

#### Scope

- Extract create, edit, status, delete, bulk mutation, editor state, and
  mutation recovery handling into `useShortLinkMutations`.
- Extract analytics dialog presentation into `ShortLinkAnalyticsDialog` while
  keeping share and QR dialogs as their existing focused components.
- Keep page-level confirmation, table, discovery, export, and dialog opening
  orchestration compatible.

#### Acceptance Criteria

- `ShortLinkAdminPage` no longer owns mutation implementations or analytics
  dialog markup.
- Existing permissions, field validation, retry context, toast copy, list
  updates, and analytics close behavior remain compatible.
- Architecture check, frontend tests, production build, and performance budget
  remain green.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/hooks/useShortLinkMutations.ts`
- `src/ShortenLink.Web/src/features/short-links/components/ShortLinkAnalyticsDialog.tsx`
- `src/ShortenLink.Web/src/features/short-links/pages/ShortLinkAdminPage.tsx`
- `src/ShortenLink.Web/test/short-link-mutations.test.ts`
- `.okf/phase/031/PHASE_SUMMARY.md`

#### Verification

```powershell
cd .\src\ShortenLink.Web
bun run check:architecture
bun test
bun run check:performance
```

#### Done Notes

- Added `useShortLinkMutations` as the single owner for short-link editor state,
  create/edit/status/delete/bulk mutation handlers, permission checks, API
  field mapping, and retry-preserving mutation context.
- Added `ShortLinkAnalyticsDialog` and reduced the page to passing analytics
  data and lifecycle callbacks; share and QR dialogs remain separate.
- Added pure mutation payload and expiry-editor regression coverage.
- Architecture boundaries passed, 73 Bun tests passed, production build
  completed, and the performance budget passed.

### 031_022 - Decompose security management mutation and dialog responsibilities

#### Step Goal

Move security management mutation orchestration and user/role dialog rendering
out of `SecurityManagementPage` while preserving routes, permissions, API
contracts, and the existing users and roles workflows.

#### Scope

- Extract user and custom-role mutation state, validation, recovery, and
  success handling into a focused security mutation hook.
- Extract create-user, user-action, and custom-role dialog presentation into a
  focused security dialog component.
- Keep discovery, table filtering, role-permission workspace behavior, and page
  access checks compatible.

#### Acceptance Criteria

- `SecurityManagementPage` no longer owns security mutation implementations or
  inline user/role dialog markup.
- Existing field validation, retry context, permission assignment, disable
  confirmation, role deletion guard, toast copy, and dirty-form behavior remain
  compatible.
- Architecture check, frontend tests, production build, and performance budget
  remain green.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/hooks/useSecurityMutations.ts`
- `src/ShortenLink.Web/src/features/short-links/components/SecurityManagementDialogs.tsx`
- `src/ShortenLink.Web/src/features/short-links/pages/SecurityManagementPage.tsx`
- `src/ShortenLink.Web/test/security-mutations.test.ts`
- `.okf/phase/031/PHASE_SUMMARY.md`

#### Verification

```powershell
cd .\src\ShortenLink.Web
bun run check:architecture
bun test
bun run check:performance
```

#### Done Notes

- Added `useSecurityMutations` as the single owner for managed-user and custom-role
  mutation state, validation, recovery notices, permission override persistence,
  disable/delete confirmations, and dirty-form state.
- Added `SecurityManagementDialogs` for create-user, user action, disable, and
  delete dialog presentation; the page now passes state and callbacks instead of
  owning the dialog markup.
- Preserved discovery, access checks, role-permission staging, API contracts,
  toast copy, and dirty-form callbacks.
- Added role form and permission override boundary regression tests.
- Architecture boundaries passed, 75 Bun tests passed, production build
  completed, and the performance budget passed.

### 031_023 - Extract security role-permission workspace presentation

#### Step Goal

Move the role-permission workspace presentation and its local search, expansion,
draft, and save-confirmation UI out of `SecurityManagementPage` while preserving
staged permission behavior and API contracts.

#### Scope

- Extract `RolePermissionMatrix` and its permission workspace helpers into a
  feature-scoped component.
- Keep role selection, permission search, group expansion, dirty-state callback,
  staged drafts, save confirmation, and permission persistence compatible.
- Keep page-level security access checks, discovery, user management, and role
  mutation ownership unchanged.

#### Foundation for Next Step

Phase 031 leaves the frontend with a feature-scoped role-permission workspace
and verified page, query, mutation, cancellation, and performance boundaries.
The next phase can build product behavior on this frontend foundation without
reopening the completed decomposition work.

#### Acceptance Criteria

- `SecurityManagementPage` no longer contains the role-permission workspace
  implementation or its presentation-only helpers.
- Role selection, permission toggles, group toggles, search, expansion, dirty
  state, save confirmation, and save callbacks behave as before.
- Architecture check, frontend tests, production build, and performance budget
  remain green.

#### Affected Files

- `src/ShortenLink.Web/src/features/short-links/components/RolePermissionMatrix.tsx`
- `src/ShortenLink.Web/src/features/short-links/pages/SecurityManagementPage.tsx`
- `src/ShortenLink.Web/test/security-role-permission.test.ts`
- `.okf/phase/031/PHASE_SUMMARY.md`

#### Verification

```powershell
cd .\src\ShortenLink.Web
bun run check:architecture
bun test
bun run build
bun run check:performance
```

#### Done Notes

- Extracted `RolePermissionMatrix` and its permission workspace helpers from
  `SecurityManagementPage` while preserving role selection, permission search,
  group expansion, staged drafts, dirty-state callbacks, save confirmation,
  and permission persistence behavior.
- Kept page-level security access checks, discovery, user management, and role
  mutation ownership unchanged.
- Architecture boundaries passed, all 77 Bun tests passed, production build
  completed successfully, and the frontend performance budget passed.
