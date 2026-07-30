---
task: 024_001
phase: 024
title: Default host request context and pluggable authorization adapter
status: done
created_at: 2026-07-30
updated_at: 2026-07-30
completed_at: 2026-07-30T11:00:00+07:00
---

# 024_001 - Default Host Request Context and Pluggable Authorization Adapter

## Step Goal

Make the reusable Hosting package self-contained for HTTP request authorization
while preserving a public DI override point for consumers with their own
identity and policy systems.

## Scope

In:

- Move the default `ICurrentRequestContext` HTTP adapter from the demo API into
  `ShortenLink.Hosting`.
- Register `IHttpContextAccessor` and the default request context through
  `TryAdd`, so a consumer can register its own context or authorization service
  before calling `AddShortenLink`.
- Keep `IShortenLinkAuthorizationService` as the public authorization adapter
  contract and document its success/unauthorized/forbidden semantics.
- Remove duplicate demo API request-context wiring and add focused registration
  coverage.
- Update README with an external-host override example.

Out:

- New authentication protocol, JWT implementation, or external identity store.
- Configurable route prefixes; that is the next task in this phase.
- Changes to ownership/share rules or Application handler contracts.

## Acceptance Criteria

- A consumer can pre-register `ICurrentRequestContext` and it is not replaced
  by `AddShortenLink`.
- A consumer can pre-register `IShortenLinkAuthorizationService` and the
  built-in session/API-key evaluator is not replaced.
- Without overrides, the demo API behavior remains unchanged.
- The default request context maps successful, unauthorized, and forbidden
  authorization results to the existing typed Core exceptions.
- No external consumer needs to reference `ShortenLink.Api` to obtain the
  default HTTP request-context adapter.
- Backend build and focused API/registration tests pass.

## Foundation for Next Step

Leaves a package-owned authorization/request boundary that configurable endpoint
mapping can consume without coupling route presentation or policy selection to
the demo API.

## Affected Files

- `.okf/phase/024/PHASE_SUMMARY.md`
- `README.md`
- `shared/ShortenLink.Hosting/HttpCurrentRequestContext.cs`
- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `src/ShortenLink.Api/HttpCurrentRequestContext.cs`
- `src/ShortenLink.Api/Program.cs`
- `tests/ShortenLink.Api.Tests/ShortLinkEndpointsTests.cs`

## Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal
```

## Done Notes

- Moved the default HTTP `ICurrentRequestContext` adapter into
  `ShortenLink.Hosting` and registered it with `TryAdd` alongside the HTTP
  context accessor.
- Preserved consumer overrides for both `ICurrentRequestContext` and
  `IShortenLinkAuthorizationService`; the demo API no longer owns a required
  request-context adapter.
- Documented the external-host registration order and authorization result
  semantics in `README.md`.
- Verification:
  - `dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers` passed with 0 warnings and 0 errors.
  - `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal --disable-build-servers` passed: 170 tests.
  - `scripts/smoke-consumer-package.ps1` was attempted but is blocked by the
    pre-existing missing `MapShortenLinkEndpoints` package API and malformed
    offline NuGet source handling; endpoint mapping is the next Phase 024 task.
