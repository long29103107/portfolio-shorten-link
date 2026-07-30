---
task: 024_002
phase: 024
title: Configurable endpoint mapping and route policy options
status: done
created_at: 2026-07-30
updated_at: 2026-07-30
completed_at: 2026-07-30
---

# 024_002 - Configurable Endpoint Mapping and Route Policy Options

## Step Goal

Expose a package-owned `MapShortenLinkEndpoints` extension so an external
ASP.NET Core host can map management and redirect routes without referencing
`ShortenLink.Api` internals.

## Scope

In:

- Add public endpoint mapping in `ShortenLink.Hosting` for the existing
  Application commands/queries and redirect flow.
- Support configurable management prefix, redirect prefix, endpoint selection,
  and optional ASP.NET Core authorization policy name.
- Keep default routes backward-compatible with `/api/short-links` and `/{code}`.
- Keep rate-limit metadata and Application authorization behavior intact.
- Switch the demo API to the package-owned mapping and update consumer smoke to
  use the current generated-code create contract.
- Add focused endpoint mapping/option tests and README documentation.

Out:

- Changing Application handlers, ownership/share rules, or security protocols.
- Adding a new controller/MVC adapter or changing response JSON contracts.
- Removing the demo-only security/admin endpoint groups.

## Acceptance Criteria

- A clean consumer can call `app.MapShortenLinkEndpoints()` after
  `AddShortenLink` without referencing `ShortenLink.Api`.
- Default mapping preserves create, list, detail, update, activate, deactivate,
  delete, and redirect routes used by the demo API.
- A consumer can configure management/redirect prefixes and disable either
  endpoint family without changing package code.
- An optional policy name is applied to mapped endpoint groups while omitted
  policy configuration preserves existing application authorization behavior.
- Existing API tests remain green and consumer smoke reaches create, detail,
  redirect, delete, and post-delete redirect checks.
- Backend build, focused tests, and package consumer verification pass.

## Foundation for Next Step

Leaves a stable host-owned route boundary for a later redirect-only profile and
provider-neutral persistence work without duplicating endpoint adapters.

## Affected Files

- `.okf/phase/024/PHASE_SUMMARY.md`
- `README.md`
- `shared/ShortenLink.Hosting/ShortenLinkEndpointOptions.cs`
- `shared/ShortenLink.Hosting/ShortenLinkEndpointMappings.cs`
- `shared/ShortenLink.Hosting/ShortenLink.Hosting.csproj`
- `src/ShortenLink.Api/Program.cs`
- `scripts/smoke-consumer-package.ps1`
- `tests/ShortenLink.Api.Tests/ShortLinkEndpointsTests.cs`

## Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal --disable-build-servers
.\scripts\smoke-consumer-package.ps1
```

## Done Notes

- Added public `MapShortenLinkEndpoints` and `ShortenLinkEndpointOptions` to
  `ShortenLink.Hosting`, including configurable management/redirect prefixes,
  endpoint-family toggles, optional authorization policy, and existing rate
  limiting metadata.
- Switched the demo API to the package-owned mapping and updated the consumer
  smoke payload to the generated-code contract.
- `dotnet build ... --no-restore` passed with zero warnings/errors.
- `dotnet test ... --no-build --no-restore` passed: 170 tests.
- Consumer smoke was attempted, but the environment could not restore the
  transitive package graph because NuGet access was unavailable; the existing
  consumer app itself started successfully when run from the built package.
