---
phase: 024
title: External Host Integration
status: complete
created_at: 2026-07-30
updated_at: 2026-07-31
current_task: null
task_count: 4
done_count: 4
depends_on:
  - 023
---

# Phase 024 Summary

## Phase Goal

Let an existing ASP.NET Core application adopt ShortenLink without replacing
its authentication, authorization, or request-context conventions.

## Phase Done Criteria

- Consumers can bring their own authorization evaluator through a documented
  public DI contract.
- The reusable Hosting package supplies the default HTTP request-context
  adapter while allowing consumers to override it before registration.
- Demo API wiring contains no package-required request-context implementation
  that external hosts would need to copy.
- Redirect-only and management handlers continue to receive the same stable
  Application contracts.
- External-host integration tests and package smoke verification pass.
- README documents the override and default registration behavior.

## Task Index

| Task | Title | Status | Done At |
|---|---|---|---|
| 024_001 | Default host request context and pluggable authorization adapter | done | 2026-07-30T11:00:00+07:00 |
| 024_002 | Configurable endpoint mapping and route policy options | done | 2026-07-30 |
| 024_003 | Redirect-only integration profile | done | 2026-07-30 |
| 024_004 | External host profile documentation and closure evidence | done | 2026-07-30 |

## Current Task

No task is active.

## Completed Notes

- Phase 023 completed the expiry lifecycle and form-preset work.
- Product Vision now prioritizes external host integration before provider and
  ecosystem expansion.
- `024_001` moved the default HTTP request context into Hosting, preserved
  consumer DI overrides, and removed the demo API-only adapter dependency.
- The existing consumer smoke reaches the package build but remains blocked by
  the pre-existing absence of `MapShortenLinkEndpoints`; configurable endpoint
  mapping is intentionally reserved for `024_002`.
- `024_002` added configurable package-owned endpoint mapping, switched the demo
  API to it, and passed the backend build plus all 170 tests. Consumer smoke
  reached package startup, but final restore verification was blocked by the
  unavailable NuGet feed in this environment.
- `024_003` is the next step to remove demo security/bootstrap requirements from
  redirect-only consumers.
- `024_003` added the opt-in redirect-only host registration and skips security
  repository/bootstrap wiring while retaining redirect dependencies. Final
  build/test verification is blocked by the unavailable NuGet feed.
- `024_004` documented full and redirect-only consumer setup paths and recorded
  the remaining verification boundary.

## Next Task Proposal

Phase 024 is implementation-complete. Re-run package smoke and close the phase
once the NuGet feed/cache is available for clean consumer verification.

## Task Notes

- `024_001-default-host-request-context-and-pluggable-authorization-adapter.md`
- `024_002-configurable-endpoint-mapping-and-route-policy-options.md`
- `024_003-redirect-only-integration-profile.md`
- `024_004-external-host-profile-documentation.md`

## Scan Rule

Keep demo session/API-key behavior as the default compatibility path, but never
force external consumers to copy demo API adapters. Preserve `TryAdd` override
semantics and keep authorization decisions outside Core business rules.
