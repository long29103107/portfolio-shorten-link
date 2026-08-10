---
phase: 040
task: 040_001
title: Bulk job contract and status tracking
status: done
created_at: 2026-08-10
started_at: 2026-08-10
depends_on:
  - 039_001
---

# 040_001 - Bulk Job Contract and Status Tracking

## Step Goal

Accept oversized bulk short-link operations without holding the HTTP request
open, then expose safe progress and partial results through a stable job id.

## Scope

In:

- A bounded process-local queue and job registry.
- Create and status endpoints for activate, deactivate, delete, and organize.
- Captured actor/tenant scope with per-item authorization during execution.
- Queued/running/completed/failed/unavailable status and result reporting.
- Frontend API/types and focused backend/frontend tests.
- README and phase bookkeeping.

Out:

- Durable job persistence, cross-process workers, cancellation endpoint, bulk
  import, and new operation vocabulary.

## Acceptance Criteria

- Job creation validates 1-1000 unique codes and returns `202` with a job id.
- Status is visible only to the submitting actor/tenant and does not expose
  request secrets or unrelated records; restart loss is represented safely as
  not-found for this process-local phase.
- Worker execution reuses existing per-item authorization and partial-result
  semantics without changing synchronous bulk behavior.
- Queue saturation returns a stable conflict response rather than dropping a
  job silently.
- Frontend can submit a job and poll status with clear recovery states.
- Required backend/frontend tests and builds pass.

## Foundation for Next Step

Leaves a stable async job contract that can later gain durable persistence or
explicit cancellation without changing the operation result vocabulary.

## Affected Files

- `.okf/phase/040/PHASE_SUMMARY.md`
- `.okf/phase/040/040_001-bulk-job-contract.md`
- `src/ShortenLink.Application/Features/ShortLinks/Bulk`
- `src/ShortenLink.Application/Abstractions`
- `shared/ShortenLink.Hosting`
- `shared/ShortenLink.Hosting/Endpoints/Map.cs`
- `src/ShortenLink.Web/src/features/short-links`
- `tests/ShortenLink.Api.Tests`
- `src/ShortenLink.Web/test`
- `README.md`

## Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal --disable-build-servers
Set-Location .\src\ShortenLink.Web
bun test
bun run build
Set-Location ..\..
git diff --check
```

## Done Notes

Implemented `POST /api/short-links/bulk/jobs` with 1-1,000 unique-code
validation and `202 Accepted`, plus actor/tenant-scoped
`GET /api/short-links/bulk/jobs/{jobId}` status polling. A bounded process-local
queue (capacity 32) runs the existing bulk executor in a scoped background
worker, preserving per-item authorization, audit behavior, and partial results.
Queue saturation returns `409 bulk_job_queue_full`; completed job retention is
bounded and restart state is intentionally unavailable by design.

Added frontend job types/routes/submit/status/poll helpers, README contract
documentation, and an API integration test covering accepted-to-completed
execution.

Verification passed:

- `dotnet build ShortenLink.slnx --no-restore`
- `dotnet test tests/ShortenLink.Api.Tests/ShortenLink.Api.Tests.csproj --no-restore --filter FullyQualifiedName~BulkJob`
- `dotnet test tests/ShortenLink.Api.Tests/ShortenLink.Api.Tests.csproj --no-build --verbosity minimal` (106 passed)
- `bun test` (84 passed)
- `bun run build`
- `git diff --check`
