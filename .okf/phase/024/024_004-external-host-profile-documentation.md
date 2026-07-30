---
task: 024_004
phase: 024
title: External host profile documentation and closure evidence
status: done
created_at: 2026-07-30
updated_at: 2026-07-30
completed_at: 2026-07-30
---

# 024_004 - External Host Profile Documentation and Closure Evidence

## Step Goal

Make the full and redirect-only host integration paths directly usable from the
README and record the remaining verification boundary for Phase 024.

## Scope

- Update the ASP.NET Core setup example to use package-owned endpoint mapping.
- Document redirect-only registration and management endpoint disabling.
- Preserve the default full profile and external authorization override guidance.

## Acceptance Criteria

- README contains runnable full-profile and redirect-only snippets.
- No demo API adapter is required by either documented path.
- Phase bookkeeping records the package verification limitation explicitly.

## Foundation for Next Step

Phase 025 can build provider-neutral persistence contracts on a documented host
integration boundary.

## Affected Files

- `README.md`
- `.okf/phase/024/PHASE_SUMMARY.md`

## Verification

`git diff --check` passed. Build/test rerun remains blocked by unavailable NuGet
dependencies in the local restore cache.

## Done Notes

- Updated the full host example to call `UseRateLimiter` and
  `MapShortenLinkEndpoints`.
- Added a redirect-only consumer example using `RedirectOnly = true` and
  `MapManagementEndpoints = false`.
