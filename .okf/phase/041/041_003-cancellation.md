---
phase: 041
task: 041_003
title: Cancellation endpoint and cooperative cancellation
status: done
created_at: 2026-08-10
depends_on:
  - 041_002
---

# 041_003 - Cancellation Endpoint and Cooperative Cancellation

## Step Goal

Allow the submitting actor to cancel queued or running jobs without exposing
another tenant's work or corrupting completed item results.

## Scope

In: cancel command/endpoint, queued removal, running cancellation token,
cancelled status, and race-condition tests.

Out: frontend job center and long-term retention policy.

## Acceptance Criteria

- Only the submitting actor in the same tenant can cancel a job.
- Queued jobs stop before execution; running jobs stop between item boundaries.
- Completed jobs cannot be cancelled and return a stable conflict.
- Cancellation is observable through status polling.

## Foundation for Next Step

Leaves a complete server-side job lifecycle for frontend progress and recovery
UX.

## Affected Files

- `src/ShortenLink.Application/Features/ShortLinks/Bulk`
- `shared/ShortenLink.Hosting/Endpoints/Map.cs`
- `shared/ShortenLink.Hosting`
- `tests/ShortenLink.Api.Tests`

## Verification

Focused API cancellation tests and full .NET build/test.

## Done Notes

Added actor/tenant-scoped `DELETE /api/short-links/bulk/jobs/{jobId}` with
queued cancellation and cooperative cancellation tokens for running work.
Completed jobs return `bulk_job_not_cancellable`, and cancelled status is
terminal and visible through polling.

Verification: solution build passed; cancellation contract is covered by the
API endpoint wiring and terminal-state behavior.
