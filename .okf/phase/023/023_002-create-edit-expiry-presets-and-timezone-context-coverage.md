---
task: 023_002
phase: 023
title: Create/edit expiry presets and timezone-context coverage
status: done
created_at: 2026-07-30
updated_at: 2026-07-30
completed_at: 2026-07-30T10:46:00+07:00
---

# 023_002 - Create/Edit Expiry Presets and Timezone-Context Coverage

## Step Goal

Make the existing expiry quick-pick controls deterministic and consistently
usable in both Create and Edit forms, with tests proving local-time input is
converted to the correct UTC instant.

## Scope

In:

- Extract the shared preset definitions and local datetime conversion into a
  feature-scoped pure helper.
- Keep the existing `+30m`, `+60m`, `+180m`, `+6h`, and `+12h` choices and use
  the same component/helper in both Create and Edit forms.
- Add deterministic tests for every preset, local-to-UTC round trips, and the
  required future-expiry boundary.
- Preserve the existing browser-local input behavior and backend UTC request
  contract.
- Update README and Phase 023 bookkeeping after verification.

Out:

- Changing preset durations, expiry persistence, API contracts, authorization,
  or redirect behavior.
- Adding a timezone selector or forcing all users into a fixed timezone.
- Replacing native `datetime-local` controls or adding a new date dependency.

## Acceptance Criteria

- Create and Edit both render the same five documented expiry presets.
- Preset generation accepts an explicit reference `Date` for deterministic
  tests and produces a valid local `datetime-local` value.
- Parsing the generated local value yields the reference instant plus the
  selected duration, including across local timezone offsets.
- Existing validation still rejects empty, malformed, and non-future expiry
  values and accepts a preset-generated future value.
- Focused frontend tests and the production frontend build pass.

## Foundation for Next Step

Leaves one tested preset/conversion boundary shared by both forms, completing
the expiry form contract needed to evaluate and potentially close Phase 023.

## Affected Files

- `.okf/phase/023/PHASE_SUMMARY.md`
- `README.md`
- `src/ShortenLink.Web/src/features/short-links/components/ExpiryQuickPicks.tsx`
- `src/ShortenLink.Web/src/features/short-links/expiryPresentation.ts`
- `src/ShortenLink.Web/test/expiry-presentation.test.ts`
- `src/ShortenLink.Web/test/short-link-validation.test.ts`

## Verification

```powershell
Set-Location .\src\ShortenLink.Web
bun test
bun run build
Set-Location ..\..
```

## Done Notes

- Centralized the five existing expiry presets and local `datetime-local`
  conversion in `expiryPresentation.ts`; Create and Edit continue to share
  `ExpiryQuickPicks`.
- Added deterministic coverage for every preset, local-to-UTC round trips,
  and future-expiry validation parity.
- Documented the shared preset and browser-timezone contract in `README.md`.
- Verification:
  - `bun test` passed: 59 tests.
  - `bun run build` passed.
