---
task: 025_001
phase: 025
title: External store registration boundary
status: done
created_at: 2026-07-30
updated_at: 2026-07-30
completed_at: 2026-07-30
---

# 025_001 - External Store Registration Boundary

## Step Goal

Allow a consumer to skip the built-in EF database path and register its own
repository and transaction implementations through the existing public Core
contracts.

## Scope

- Add a public host option for external persistence.
- Skip EF DbContext, built-in repositories, and database initialization when enabled.
- Keep the default registration behavior unchanged.
- Document provider responsibilities.

## Acceptance Criteria

- `UseExternalPersistence = true` does not require a database connection string.
- Built-in defaults still register when the option is false.
- External consumers can provide `IShortLinkRepository`, `IUnitOfWork`, click,
  share, and audit repositories through DI.

## Foundation for Next Step

Leaves a stable registration seam for a provider contract-test fixture.

## Affected Files

- `.okf/phase/025/PHASE_SUMMARY.md`
- `README.md`
- `shared/ShortenLink.Hosting/ShortenLinkHostOptions.cs`
- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`

## Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers
```

## Done Notes

- Added `UseExternalPersistence` to the public host options.
- External mode skips EF DbContext, built-in repository registrations, security
  bootstrap, and database initialization so consumers can provide Core
  repository/transaction contracts through DI.
- Default SQLite/PostgreSQL registration remains unchanged.
- `git diff --check` passed; build verification is blocked by the unavailable
  NuGet packages in the current local restore cache.
