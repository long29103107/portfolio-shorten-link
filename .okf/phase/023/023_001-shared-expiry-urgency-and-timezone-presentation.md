---
task: 023_001
phase: 023
title: Shared expiry urgency and timezone presentation
status: done
created_at: 2026-07-30
updated_at: 2026-07-30
completed_at: 2026-07-30T10:44:00+07:00
---

# 023_001 - Shared Expiry Urgency and Timezone Presentation

## Step Goal

Give users one deterministic, reusable expiry presentation contract that makes
the displayed timezone explicit and distinguishes active links approaching
expiry from healthy, expired, inactive, and deleted links.

## Scope

In:

- Add a feature-scoped expiry presentation helper that accepts an explicit
  reference time and classifies a link without reading the wall clock
  internally.
- Reuse the existing short-link lifecycle/status data; do not derive a
  client-only replacement for backend status.
- Show clear timezone context wherever expiry is displayed in the short-link
  list and detail view.
- Add accessible text and restrained visual treatment for active links that
  will expire within a documented threshold.
- Add focused deterministic tests for threshold boundaries, status precedence,
  invalid/missing dates, timezone labeling, and presentation output.
- Update README and Phase 023 bookkeeping when the implementation is verified.

Out:

- Changing expiry persistence, API contracts, authorization, redirect behavior,
  or backend validation.
- Notifications, scheduled jobs, automatic renewal, or expiry mutation.
- A new date/time dependency when platform date APIs and existing project
  helpers are sufficient.
- New discovery filters or sorting behavior; those can build on this shared
  classification only after it is verified.
- Replacing the existing `ExpiryQuickPicks` form boundary.

## Acceptance Criteria

- One pure helper classifies expiry using the supplied reference time and a
  named, documented soon-expiry threshold.
- Inactive, deleted, and expired lifecycle states take precedence over an
  expiring-soon cue; only active future links can be labeled expiring soon.
- Boundary behavior at the threshold is deterministic and covered by tests.
- List and detail expiry values communicate the displayed timezone or offset
  without requiring users to infer it.
- Expiring-soon treatment includes readable text and does not rely on color
  alone.
- Existing create/edit validation, lifecycle actions, authorization, and
  redirect behavior remain unchanged.
- Focused frontend tests pass and `bun run build` succeeds.

## Foundation for Next Step

Leaves a tested expiry classification and formatting boundary that a later
Phase 023 task can reuse to refine form presets or add expiry-focused discovery
without duplicating time calculations or presentation semantics.

## Affected Files

Expected starting points:

- `.okf/phase/023/PHASE_SUMMARY.md`
- `README.md`
- `src/ShortenLink.Web/src/features/short-links/types.ts`
- `src/ShortenLink.Web/src/features/short-links/pages/ShortLinkAdminPage.tsx`
- `src/ShortenLink.Web/src/features/short-links/pages/ShortLinkDetailPage.tsx`
- `src/ShortenLink.Web/src/features/short-links/`
- `src/ShortenLink.Web/src/styles.css`
- `src/ShortenLink.Web/test/`

Implemented files:

- `src/ShortenLink.Web/src/features/short-links/expiryPresentation.ts`
- `src/ShortenLink.Web/test/expiry-presentation.test.ts`
- `src/ShortenLink.Web/src/features/short-links/pages/ShortLinkAdminPage.tsx`
- `src/ShortenLink.Web/src/features/short-links/pages/ShortLinkDetailPage.tsx`
- `src/ShortenLink.Web/src/styles.css`
- `README.md`

## Verification

Run after implementation:

```powershell
Set-Location .\src\ShortenLink.Web
bun test
bun run build
Set-Location ..\..
```

Exercise exact threshold, just-inside, just-outside, already-expired, inactive,
deleted, malformed-date, and explicit-timezone presentation cases with fixed
reference times.

## Done Notes

- Added `expiryPresentation.ts` with an explicit reference-time seam, a named
  24-hour threshold, lifecycle precedence, and timezone-aware formatting.
- Added expiry list/detail presentation with readable status/detail text and
  non-color-only expiring-soon cues while preserving backend contracts.
- Documented the local-timezone and 24-hour presentation behavior in
  `README.md`.
- Verification:
  - `bun test` passed: 57 tests.
  - `bun run build` passed.
