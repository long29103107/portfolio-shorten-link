---
phase: 031
title: Frontend Source Optimization
status: active
created_at: 2026-08-07
updated_at: 2026-08-07
current_task: null
task_count: 8
done_count: 8
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

## Current Task

`031_008` is complete, but Phase 031 is not yet complete. The eight tasks now
provide feature boundaries, lazy route loading, cancellable discovery, a typed
API client, centralized frontend contracts, and a dashboard data boundary.
Remaining work is further page decomposition, broader query cancellation
coverage, and automated bundle/performance checks.

## Next Task Proposal

The previously proposed `031_006` API abstraction and the dashboard data
boundary are complete. The next proposal is to extract audit discovery state
and extend query cancellation/performance guards across the remaining pages.

The stale line below is retained as historical context and is superseded by
the proposal above.

Next proposed task: `031_006` — complete page UI decomposition and extend
query cancellation/performance guards across all discovery pages.

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
