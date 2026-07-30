---
task: 025_002
phase: 025
title: Repository provider contract-test fixture
status: done
created_at: 2026-07-30
updated_at: 2026-07-30
completed_at: 2026-07-30
---

# 025_002 - Repository Provider Contract-Test Fixture

## Step Goal

Provide a reusable test fixture that every persistence adapter can inherit to
verify round-trip, update, and delete semantics of `IShortLinkRepository`.

## Scope

- Add an abstract xUnit contract fixture with a repository factory seam.
- Cover find/exists, mutable-state update, and delete/index consistency.
- Keep provider-specific setup outside the shared fixture.

## Acceptance Criteria

- A provider test can inherit the fixture and implement one factory method.
- Assertions exercise only public Core repository contracts.
- The fixture does not depend on EF Core or demo API code.

## Foundation for Next Step

Leaves a common lifecycle contract for SQLite, PostgreSQL, and future external
provider adapters.

## Affected Files

- `.okf/phase/025/PHASE_SUMMARY.md`
- `tests/ShortenLink.Core.Tests/Contracts/ShortLinkRepositoryContractTests.cs`

## Verification

`git diff --check` passed. Full test execution is blocked by unavailable NuGet
dependencies in the current local restore cache.

## Done Notes

- Added the provider-neutral abstract repository contract fixture with three
  lifecycle tests and no EF/demo dependencies.
