---
task: 024_003
phase: 024
title: Redirect-only integration profile
status: done
created_at: 2026-07-30
updated_at: 2026-07-30
completed_at: 2026-07-30
---

# 024_003 - Redirect-only Integration Profile

## Step Goal

Allow an external host to register ShortenLink for redirect resolution without
registering demo security repositories or creating a bootstrap admin account.

## Scope

In:

- Add a public host registration option for redirect-only mode.
- Skip security persistence registrations and bootstrap initialization in that mode.
- Keep database, cache, analytics, redirect handler, and rate limiting available.
- Document the profile and add focused registration coverage.

Out:

- Changing redirect business rules or response contracts.
- Removing the default full management profile.
- Adding a separate package or persistence provider.

## Acceptance Criteria

- Existing `AddShortenLink(configuration)` behavior remains unchanged.
- A consumer can call `AddShortenLink(configuration, options => options.RedirectOnly = true)`.
- Redirect-only registration does not require security repository services or bootstrap admin initialization.
- Default endpoint mapping can map only redirect routes for the profile.
- Build and focused tests pass.

## Foundation for Next Step

Leaves a minimal host registration boundary for provider-neutral persistence and
external integration samples.

## Affected Files

- `.okf/phase/024/PHASE_SUMMARY.md`
- `README.md`
- `shared/ShortenLink.Hosting/ShortenLinkHostOptions.cs`
- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `shared/ShortenLink.Hosting/ShortLinkDatabaseInitializationService.cs`
- `tests/ShortenLink.Api.Tests/ShortLinkEndpointsTests.cs`

## Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal --disable-build-servers
```

## Done Notes

- Added `ShortenLinkHostOptions.RedirectOnly` and an overload of
  `AddShortenLink` for minimal external hosts.
- Redirect-only registration omits security repositories/session services and
  skips bootstrap-admin initialization while retaining persistence, analytics,
  cache, rate limiting, and redirect services.
- Verification was attempted, but the local NuGet cache became incomplete after
  the earlier smoke restore; build/test are explicitly blocked by unavailable
  NuGet packages rather than a reported compiler error.
