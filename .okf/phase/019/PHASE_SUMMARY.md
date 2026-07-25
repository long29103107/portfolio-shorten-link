---
phase: 019
title: Admin Observability and Operational Health
status: complete
created_at: 2026-07-24
updated_at: 2026-07-24
current_task: null
task_count: 2
done_count: 2
completed_at: 2026-07-24
depends_on:
  - 018
---

# Phase 019 Summary

## Phase Goal

Give administrators an accurate, resilient operational view of short-link activity, identity posture, and service health.

## Phase Done Criteria

- Dashboard metrics use authoritative totals rather than client-page approximations.
- Link activity and identity status are visible at a glance.
- Partial upstream failures remain contextual and do not blank healthy metrics.
- Refresh, loading, empty, degraded, and operational states are clear.
- Dashboard behavior has focused tests and the production frontend build passes.

## Task Index

| Task | Title | Status | Done At |
|---|---|---|---|
| 019_001 | Accurate dashboard snapshot and partial health | done | 2026-07-24 |
| 019_002 | Recent operational activity snapshot | done | 2026-07-24 |

## Current Task

No active task. Phase 019 is complete with both tasks verified.

## Completed Notes

- `019_001` replaced approximate dashboard counts with authoritative totals, added identity posture metrics, and preserved healthy data during partial failures.
- Frontend verification passed with 40 tests and a successful production build.
- The phase now consumes the finalized security foundation: system roles are Admin/User only, every link is creator-owned, User visibility is owner/share scoped, per-link sharing uses View/Edit, and Admin bypasses ownership/share restrictions.
- `019_002` added a newest-first operational creation snapshot from authoritative link/user records, preserved healthy activity during partial failures, and explicitly distinguished the view from a durable audit log.
- Phase closure verification: 41 frontend tests and the TypeScript/Vite production build passed.

## Current-State Addendum

- The backend now has an explicit `ShortenLink.Application` layer and a
  dependency-free `shared/ShortenLink.Mediator`.
- Security session, personal API-key, role, user, credential-assignment,
  short-link list/detail/analytics/share/lifecycle, and redirect endpoints now
  dispatch commands or queries through `ISender`.
- Each use case keeps its command/query and handler in one feature file.
  `ShortenLinkEndpointHandlers.cs` was removed.
- Application handlers return data and throw typed Core exceptions. Stable
  error codes are centralized in `ErrorCodes`/`ShortLinkErrorCodes`, while
  `GlobalExceptionHandler` owns HTTP status and error-response mapping.
- Authorization preserves session, user-owned API-key, and configured/persisted
  assignment actors. Admin bypasses ownership/share; User visibility and
  mutation remain owner/share scoped.
- The React login flow now returns to `/`. The create-result copy action is a
  compact right-aligned icon with reusable `PortalTooltip` copied feedback.
- Current verification after the architecture migration: backend build passed
  with 0 warnings, all 153 backend tests passed, and the frontend TypeScript/Vite
  production build passed.

## Next Task Proposal

Phase 020 is open with `020_001 - Persisted short-link mutation audit and scoped query API` as its first planned task.

## Task Notes

Active and planned task notes live in separate `PPP_TTT-*.md` files.

## Scan Rule

Reuse existing list/security APIs and shared feedback primitives before adding new observability endpoints. Operational data must preserve the Admin/User boundary: Admin may aggregate globally, while User-facing data remains owner/share scoped.
