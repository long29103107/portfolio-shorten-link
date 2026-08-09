---
phase: 032
title: Frontend Application Shell Decomposition
status: complete
created_at: 2026-08-09
updated_at: 2026-08-09
current_task: null
task_count: 2
done_count: 2
depends_on:
  - 031
---

# Phase 032 Summary

## Phase Goal

Improve application-shell cohesion by separating session lifecycle,
navigation-guard orchestration, and shell presentation from route/page rendering
while preserving authentication behavior, route contracts, and user-facing
workspace behavior.

## Phase Done Criteria

- Authentication/session bootstrap and auth-change handling have a focused
  ownership boundary.
- Navigation, browser-history handling, and unsaved-change protection have a
  focused ownership boundary.
- The application shell remains a thin route/rendering facade without changing
  routes, API contracts, or user-facing behavior.
- Frontend architecture checks, tests, production build, and performance budget
  remain green after each task.

## Scope

In:

- Refactor `src/ShortenLink.Web/src/app/App.tsx` into focused app-level hooks
  and components where ownership is clear.
- Preserve login/logout, session validation, route transitions, and dirty-form
  navigation protection.

Out:

- New authentication or authorization behavior.
- Route, API, storage-key, or user-facing copy changes.
- A new state-management library or broad visual redesign.

## Task Index

| Task | Title | Category | Status | Done At |
|---|---|---|---|---|
| 032_001 | Extract application session and navigation orchestration | Refactor | done | 2026-08-09 |
| 032_002 | Extract application shell and sidebar presentation | Refactor | done | 2026-08-09 |

## Current Task

`032_002` is complete. It extracts shell chrome and sidebar/account
presentation from `App.tsx` while preserving the existing application routes,
authentication flow, permissions, and navigation behavior.

## Next Task Proposal

Phase 032 is complete. Select the next phase from the product roadmap after
reviewing the completed shell/session/navigation boundaries.

## Task Notes

Historical and active task detail is compacted here so the phase remains in one
file.

### 032_001 - Extract application session and navigation orchestration

#### Step Goal

Move authentication/session lifecycle and browser navigation/unsaved-change
guard orchestration out of `App.tsx` into focused app-level hooks while
preserving route behavior and the post-login recent-result reset.

#### Dependency

- Phase 031 frontend boundaries and performance checks are complete.
- Existing `App.tsx` route parsing, auth transport, and dirty-state behavior are
  the source contracts for this behavior-preserving refactor.

#### Scope

In:

- Extract session bootstrap, current-user synchronization, auth-change
  handling, and session invalidation ownership.
- Extract route transition, `popstate`, `pushState`, and unsaved-change guard
  ownership.
- Keep route/page rendering and existing application behavior in `App.tsx`.
- Preserve clearing `recentLink` after successful login.

Out:

- Changes to login APIs, refresh-token storage, route paths, or authorization
  policy.
- New navigation features, visual redesign, or state-library adoption.

#### Acceptance Criteria

- `App.tsx` no longer owns the session lifecycle and navigation-guard effect
  implementations.
- Login, logout, session validation failure, browser back/forward, and dirty
  form discard behavior remain compatible.
- Successful login still clears the stale right-side recent-result panel.
- The extracted boundaries are app-scoped and do not introduce feature-to-app
  or cross-feature dependency violations.
- Architecture check, frontend tests, production build, and performance budget
  pass.

#### Foundation for Next Step

The application has a stable session/navigation boundary, leaving the route
rendering facade ready for a later, independently verifiable shell/sidebar
presentation extraction if that remains useful.

#### Relevant Standards

- `.okf/standards/architecture.md`
- `.okf/standards/coding-style.md`
- `.okf/standards/testing.md`
- `PRODUCT_VISION.md`

#### Affected Files

- `src/ShortenLink.Web/src/app/App.tsx`
- `src/ShortenLink.Web/src/app/hooks/useAppSession.ts`
- `src/ShortenLink.Web/src/app/hooks/useAppNavigation.ts`
- `src/ShortenLink.Web/test/app-navigation.test.ts`
- `.okf/phase/032/PHASE_SUMMARY.md`

#### Verification

```powershell
cd .\src\ShortenLink.Web
bun run check:architecture
bun test
bun run build
bun run check:performance
```

#### Done Notes

- Added `useAppSession` for current-user synchronization, auth-change handling,
  session validation, invalidation, and sign-out ownership.
- Added `useAppNavigation` for route state, browser history, authentication
  redirects, unsaved-change protection, and discard navigation.
- Reduced `App.tsx` to route/page rendering and shell composition while
  preserving the post-login recent-result reset.
- Added three pure navigation regression tests.
- Architecture boundaries passed, all 80 Bun tests passed, production build
  completed successfully, and the frontend performance budget passed.

### 032_002 - Extract application shell and sidebar presentation

#### Step Goal

Move sidebar, account-menu, topbar, and navigation-icon presentation out of
`App.tsx` into focused app-scoped components while preserving route rendering
and existing user-facing behavior.

#### Dependency

- `032_001` provides focused session and navigation orchestration hooks.
- Existing `App.tsx` shell markup is the source contract for navigation,
  permissions, account actions, and layout classes.

#### Scope

In:

- Extract the non-home sidebar and its navigation/session presentation.
- Extract the home account menu and non-home topbar presentation.
- Keep existing callbacks, route paths, permission checks, account actions,
  layout classes, and page-title copy unchanged.

Out:

- Route, API, authentication, authorization-policy, or storage changes.
- Visual redesign, new navigation behavior, or new state-management libraries.

#### Acceptance Criteria

- `App.tsx` no longer owns sidebar, account-menu, topbar, or navigation-icon
  presentation markup.
- Login, logout, permissions, route transitions, dirty-form guards, and the
  post-login recent-result reset remain compatible.
- Extracted components stay app-scoped and pass the existing architecture
  boundaries.
- Architecture check, frontend tests, production build, and performance budget
  pass.

#### Foundation for Next Step

The application shell and route/page rendering facade have explicit ownership
boundaries, allowing Phase 032 to close when verification is green.

#### Relevant Standards

- `.okf/standards/architecture.md`
- `.okf/standards/coding-style.md`
- `.okf/standards/testing.md`
- `PRODUCT_VISION.md`

#### Affected Files

- `src/ShortenLink.Web/src/app/App.tsx`
- `src/ShortenLink.Web/src/app/components/AppSidebar.tsx`
- `src/ShortenLink.Web/src/app/components/AppHomeAccountMenu.tsx`
- `src/ShortenLink.Web/src/app/components/AppTopbar.tsx`
- `.okf/phase/032/PHASE_SUMMARY.md`

#### Verification

```powershell
cd .\src\ShortenLink.Web
bun run check:architecture
bun test
bun run build
bun run check:performance
```

#### Done Notes

- Added `AppSidebar` for brand, navigation, session, account actions, and
  navigation icons.
- Added `AppHomeAccountMenu` and `AppTopbar` for the remaining shell chrome.
- Reduced `App.tsx` to route/session/navigation composition and page rendering
  without changing route paths, permissions, logout behavior, or the recent
  result reset after login.
- Architecture check passed, all 80 Bun tests passed, production build
  completed successfully, and the frontend performance budget passed.
