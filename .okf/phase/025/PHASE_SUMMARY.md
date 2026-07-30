---
phase: 025
title: Provider-Neutral Persistence
status: active
created_at: 2026-07-30
updated_at: 2026-07-30
current_task: 025_002
task_count: 2
done_count: 1
depends_on:
  - 024
---

# Phase 025 Summary

## Phase Goal

Let external hosts replace the built-in EF persistence path while preserving
the public repository and transaction contracts used by Application.

## Phase Done Criteria

- External stores can be registered without EF database bootstrap.
- Built-in SQLite/PostgreSQL registration remains the default.
- Repository and transaction contracts are documented for providers.
- Provider contract verification exists for core link lifecycle behavior.

## Task Index

| Task | Title | Status | Done At |
|---|---|---|---|
| 025_001 | External store registration boundary | done | 2026-07-30 |
| 025_002 | Repository provider contract-test fixture | active | - |

## Current Task

`025_002` is active and adds the reusable repository provider contract fixture.

## Next Task Proposal

Add a provider contract-test fixture covering create, resolve, expiry, and
concurrency semantics.

## Task Notes

- `025_001-external-store-registration-boundary.md`
- `025_002-repository-provider-contract-test-fixture.md`
