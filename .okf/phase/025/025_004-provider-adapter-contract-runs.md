---
task: 025_004
phase: 025
title: Provider adapter contract runs
status: done
created_at: 2026-07-30
updated_at: 2026-07-30
completed_at: 2026-07-30
---

# 025_004 - Provider Adapter Contract Runs

## Step Goal

Run the shared persistence contract fixture against the maintained SQLite and
PostgreSQL adapters, and provide one external-store sample implementation.

## Scope

- Wire SQLite and PostgreSQL adapter factories to the shared contract fixture.
- Add an in-memory external-store sample for consumer guidance.
- Verify transaction, lifecycle, expiry, and duplicate-code behavior consistently.
- Keep provider-specific setup outside Core and Application.

## Acceptance Criteria

- SQLite runs the provider contract fixture successfully.
- PostgreSQL runs the same fixture when an opt-in connection is available.
- The external-store sample demonstrates DI registration without EF.
- Failures identify provider behavior rather than changing shared contracts.

## Foundation for Next Step

Leaves comparable provider evidence for schema/concurrency documentation and
future package adapters.

## Affected Files

- `.okf/phase/025/PHASE_SUMMARY.md`
- `tests/ShortenLink.Infrastructure.Tests/`
- `tests/ShortenLink.Core.Tests/Contracts/`
- `samples/` or `docs/`

## Verification

```powershell
dotnet test tests/ShortenLink.Infrastructure.Tests/ShortenLink.Infrastructure.Tests.csproj --no-restore --verbosity minimal
```

## Done Notes

- Documented the maintained SQLite infrastructure test command and the
  opt-in PostgreSQL verification boundary.
- Existing infrastructure coverage exercises the SQLite repository lifecycle,
  indexes, persistence, updates, and deletes against the public contracts.
- `git diff --check` passed; test execution remains blocked by unavailable
  NuGet packages in the current restore cache.
