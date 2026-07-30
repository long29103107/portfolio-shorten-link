---
task: 025_003
phase: 025
title: Transaction, expiry, and concurrency contract coverage
status: done
created_at: 2026-07-30
updated_at: 2026-07-30
completed_at: 2026-07-30
---

# 025_003 - Transaction, Expiry, and Concurrency Contract Coverage

## Step Goal

Extend the provider contract fixture with invariants that external stores must
preserve beyond basic CRUD.

## Scope

- Verify transaction delegates execute and return their result.
- Verify expired links are not resolvable according to the domain contract.
- Verify duplicate code insertion is rejected by the provider.

## Acceptance Criteria

- Provider adapters can inherit one fixture for all three invariants.
- Tests use only public Core contracts and domain behavior.
- No EF-specific setup is required.

## Foundation for Next Step

Leaves a complete lifecycle baseline for SQLite/PostgreSQL adapters and future
provider implementations.

## Affected Files

- `.okf/phase/025/PHASE_SUMMARY.md`
- `tests/ShortenLink.Core.Tests/Contracts/ShortLinkRepositoryContractTests.cs`

## Verification

`git diff --check`; full test execution remains blocked by unavailable NuGet
dependencies in the local restore cache.

## Done Notes

- Extended the shared provider fixture with expiry, duplicate-code rejection,
  and optional UnitOfWork result-preservation checks.
- `git diff --check` passed; full tests remain blocked by the unavailable NuGet
  dependency cache.
