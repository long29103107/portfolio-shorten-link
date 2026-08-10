---
phase: 041
task: 041_001
title: Durable job persistence and restart recovery
status: done
created_at: 2026-08-10
started_at: 2026-08-10
depends_on:
  - 040_001
---

# 041_001 - Durable Job Persistence and Restart Recovery

## Step Goal

Persist bulk job metadata, status, ownership, progress, and result so queued
jobs and completed results survive application restarts.

## Scope

In: EF entity/configuration/repository, SQLite/PostgreSQL schema creation,
durable worker claims, restart recovery, status API integration, and tests.

Out: retry policy, cancellation API, frontend job center, and retention policy.

## Acceptance Criteria

- Submitted jobs are persisted before `202` is returned.
- Status remains readable after a new application scope/process starts.
- Queued jobs can be claimed by the worker; interrupted running jobs become a
  safe failed/recoverable state.
- Actor and tenant ownership remain enforced.
- No raw credentials or secrets are persisted.

## Foundation for Next Step

Leaves a durable job record and claim boundary for retries and idempotency.

## Affected Files

- `src/ShortenLink.Infrastructure/Persistence`
- `shared/ShortenLink.Hosting`
- `src/ShortenLink.Application/Features/ShortLinks/Bulk`
- `tests/ShortenLink.Infrastructure.Tests`
- `tests/ShortenLink.Api.Tests`

## Verification

`dotnet build ShortenLink.slnx --no-restore`; focused persistence/API tests;
`dotnet test ShortenLink.slnx --no-build`.

## Done Notes

Added `short_link_bulk_jobs` persistence with SQLite/PostgreSQL schema support,
status/progress/result/ownership fields, durable enqueue-before-202 behavior,
worker claim and restart recovery, and actor/tenant-scoped status reads. Job
submission bypasses the outer request UnitOfWork because it owns its durable
commit and must not contend with a second SQLite transaction.

Verification passed: `dotnet build ShortenLink.slnx --no-restore` and the
bulk-job API integration test, including durable idempotency fields and worker
completion.
