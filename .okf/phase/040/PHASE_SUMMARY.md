---
phase: 040
title: Asynchronous Bulk Operations
status: complete
created_at: 2026-08-10
updated_at: 2026-08-10
completed_at: 2026-08-10
current_task: null
task_count: 1
done_count: 1
depends_on:
  - 039
---

# Phase 040 Summary

## Phase Goal

Move oversized bulk short-link operations behind a bounded asynchronous job
boundary while preserving the existing operation vocabulary, per-item access
checks, and partial-result reporting.

## Phase Done Criteria

- Authorized callers can submit a validated bulk job and receive a stable job id.
- Job status reports queued, running, completed, or failed states, with safe
  not-found behavior after process restart, without exposing another user's
  job or request secrets.
- Background execution reuses the existing bulk operation semantics and result
  shape, including independent authorization and partial failures.
- Queue capacity, code limits, cancellation behavior, tests, builds, and docs
  are explicit and verified.

## Task Index

| Task | Title | Category | Status | Done At |
|---|---|---|---|---|
| 040_001 | Bulk job contract and status tracking | Feature | done | 2026-08-10 |

## Current Task

`040_001` is complete and builds on Phase 039's bounded bulk contract.

## Completed Notes

- Phase 039 delivered synchronous bulk lifecycle, organization, partial-result,
  and selected-export behavior.
- Phase 040 delivers the bounded process-local async bulk job contract, worker,
  actor-scoped status endpoint, frontend polling client, documentation, and
  verification coverage.

## Next Task Proposal

No next-task proposal. Durable job persistence or explicit cancellation should
be proposed as a separate phase if process-local retention is no longer enough.

## Task Notes

See [040_001-bulk-job-contract.md](040_001-bulk-job-contract.md).

## Scan Rule

Keep the async boundary bounded and process-local for this phase. Do not add
durable job storage or cross-tenant visibility without a separately approved
task.
