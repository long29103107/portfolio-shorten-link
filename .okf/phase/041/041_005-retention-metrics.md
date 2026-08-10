---
phase: 041
task: 041_005
title: Retention, metrics, and operational visibility
status: done
created_at: 2026-08-10
depends_on:
  - 041_004
---

# 041_005 - Retention, Metrics, and Operational Visibility

## Step Goal

Bound durable job growth and make queue health, latency, retries, failures, and
cancellation visible to operators.

## Scope

In: retention options/cleanup worker, metrics, structured logs, health-check
signals, and operational tests/docs.

Out: distributed scheduling and external workflow orchestration.

## Acceptance Criteria

- Completed/failed/cancelled jobs expire according to configurable retention.
- Cleanup cannot remove queued or running jobs.
- Queue depth, duration, retry, failure, and cancellation signals are exposed
  through existing observability conventions.
- README/configuration documents safe production defaults.

## Foundation for Next Step

Closes Phase 041 with a production-grade, bounded bulk job lifecycle.

## Affected Files

- `shared/ShortenLink.Hosting/Options`
- `shared/ShortenLink.Hosting`
- `src/ShortenLink.Application`
- `tests`
- `README.md`

## Verification

Full .NET build/test, frontend checks, and `git diff --check`.

## Done Notes

Added configurable retention cleanup for terminal jobs, a maintenance hosted
service, structured cleanup logging, and `ShortenLink.BulkJobs` meter counters
for submitted/completed/failed/cancelled/retried jobs and queue depth.

Verification passed: full solution build, focused bulk-job API test, frontend
tests/build, and diff whitespace validation.
