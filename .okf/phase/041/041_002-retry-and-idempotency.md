---
phase: 041
task: 041_002
title: Retry policy and idempotent submission
status: done
created_at: 2026-08-10
depends_on:
  - 041_001
---

# 041_002 - Retry Policy and Idempotent Submission

## Step Goal

Make transient worker failures retryable and prevent client retries from
creating duplicate bulk jobs.

## Scope

In: idempotency key contract, durable uniqueness, retry counters/backoff,
terminal failure behavior, and tests.

Out: cancellation UX and frontend job center.

## Acceptance Criteria

- Reusing an idempotency key with the same request returns the original job.
- Reusing it with a different request is rejected safely.
- Transient failures retry within a bounded attempt count; permanent failures
  become terminal with a useful status.

## Foundation for Next Step

Leaves a stable execution state machine that cancellation can interrupt.

## Affected Files

- `src/ShortenLink.Application/Features/ShortLinks/Bulk`
- `shared/ShortenLink.Hosting`
- `tests/ShortenLink.Api.Tests`

## Verification

Focused API/worker tests and full .NET build/test.

## Done Notes

Added optional job idempotency keys with request fingerprint validation and
stable replay behavior. Added bounded transient retry handling with configurable
attempt count and delay; permanent failures remain terminal.

Verification: full solution build and bulk-job API integration coverage passed.
