---
phase: 023
title: Expiry Lifecycle Clarity
status: complete
created_at: 2026-07-30
updated_at: 2026-07-30
current_task: null
task_count: 2
done_count: 2
completed_at: 2026-07-30
depends_on:
  - 022
---

# Phase 023 Summary

## Phase Goal

Make short-link expiry predictable and easy to scan across creation, editing,
list, and detail workflows by using one tested lifecycle vocabulary, explicit
timezone context, and accessible urgency cues.

## Phase Done Criteria

- Create and edit expiry controls use clear, reusable presets without changing
  the required future-expiry backend contract.
- Dates expose understandable timezone context instead of presenting ambiguous
  local timestamps.
- Active links that are approaching expiry are visually and textually distinct
  from healthy, expired, inactive, and deleted links.
- Expiry presentation and form behavior share tested deterministic helpers
  rather than duplicating wall-clock calculations across components.
- Focused frontend tests and the production frontend build pass.
- README and phase bookkeeping describe the shipped expiry behavior.

## Task Index

| Task | Title | Status | Done At |
|---|---|---|---|
| 023_001 | Shared expiry urgency and timezone presentation | done | 2026-07-30T10:44:00+07:00 |
| 023_002 | Create/edit expiry presets and timezone-context coverage | done | 2026-07-30T10:46:00+07:00 |

## Current Task

No task is active. Both Phase 023 tasks are complete and verified.

## Completed Notes

- Phase 022 completed the remaining QR, filtered CSV export, and rate-limit
  visibility work identified by the product vision.
- The frontend already has reusable quick-pick buttons for create and edit;
  Phase 023 will extend that existing boundary instead of introducing a
  parallel expiry form implementation.
- `023_001` added deterministic expiry classification and timezone-aware
  presentation for list and detail views, including accessible expiring-soon
  cues.
- `023_002` centralized the five Create/Edit expiry presets and local-to-UTC
  conversion, with deterministic validation and timezone round-trip coverage.

## Next Task Proposal

Phase 023 is complete. Review the product vision before opening a subsequent
phase; do not pre-create another task here.

## Task Notes

The task notes live in:

- `023_001-shared-expiry-urgency-and-timezone-presentation.md`
- `023_002-create-edit-expiry-presets-and-timezone-context-coverage.md`

## Scan Rule

Reuse the existing `ExpiryQuickPicks`, date-formatting, status, and form
validation boundaries. Keep expiry required and future-dated, use injectable
or explicit reference times in tests, and do not infer authorization or mutate
server lifecycle behavior in the client.
