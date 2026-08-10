---
phase: 041
task: 041_004
title: Frontend job center and progress UX
status: done
created_at: 2026-08-10
depends_on:
  - 041_003
---

# 041_004 - Frontend Job Center and Progress UX

## Step Goal

Expose submitted bulk jobs in a compact job center with progress, completion,
failure, retry, and cancellation recovery states.

## Scope

In: job state store, polling lifecycle, progress UI, retry/cancel actions,
navigation-safe cleanup, and frontend tests.

Out: new backend job semantics.

## Acceptance Criteria

- A submitted job appears without blocking the short-link table.
- Progress and partial failures are readable and polling stops at terminal state.
- Retry and cancel actions preserve safe error/recovery messaging.
- Reload behavior uses the durable status API rather than losing all jobs.

## Foundation for Next Step

Leaves user-facing workflows ready for retention and operational telemetry.

## Affected Files

- `src/ShortenLink.Web/src/features/short-links`
- `src/ShortenLink.Web/test`

## Verification

`bun test` and `bun run build`.

## Done Notes

Added a persisted browser job center with submit, polling, progress, cancel,
retry, terminal failure, and reload recovery states. The short-link admin page
now exposes background bulk submission without blocking table operations.

Verification passed: `bun test` (84 passed) and `bun run build`.
