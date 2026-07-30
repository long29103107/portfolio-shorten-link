---
phase: 022
title: Product Polish and Operational Visibility
status: complete
created_at: 2026-07-29
updated_at: 2026-07-29
current_task: null
task_count: 3
done_count: 3
completed_at: 2026-07-29
depends_on:
  - 021
---

# Phase 022 Summary

## Phase Goal

Close the remaining product-vision polish and operations gaps with safe,
permission-preserving workspace capabilities for QR sharing, filtered short-link
export, and rate-limit visibility.

## Phase Done Criteria

- Every authorized short link has a usable QR presentation without exposing
  secrets or changing redirect behavior.
- Admin can export the currently filtered, authorized short-link list as a
  deterministic CSV without losing pagination coverage or adding unauthorized
  records.
- Admin can inspect configured rate-limit activity and recent throttling in the
  workspace without exposing credentials or request-sensitive data.
- Focused frontend/backend tests, production frontend build, backend build, and
  full backend tests pass for the completed slices.
- README and phase bookkeeping accurately describe the shipped capabilities.

## Task Index

| Task | Title | Status | Done At |
|---|---|---|---|
| 022_001 | Bulk CSV export for filtered admin lists | done | 2026-07-29T14:14:30+07:00 |
| 022_002 | QR code presentation for short links | done | 2026-07-29T20:15:00+07:00 |
| 022_003 | Rate-limit visibility for the admin workspace | done | 2026-07-29T20:45:00+07:00 |

## Current Task

No active task. All three Phase 022 tasks are complete and verified; Phase 022
is complete.

## Completed Notes

- Phase 021 delivered the durable audit trail and investigation workspace.
- Product vision review identified QR generation, CSV export, and rate-limit
  visibility as the remaining explicit gaps.
- `022_002` added client-side QR presentation for every authorized short-link
  row. It encodes only the returned public short URL, supports retry and PNG
  download, and leaves backend authorization and redirect behavior unchanged.
- `022_003` added Admin-only rate-limit visibility with bounded process-local
  policy rejection activity, safe configuration output, and dashboard recovery
  states without exposing request-sensitive data.

## Next Task Proposal

No next task in Phase 022. The product vision can be reviewed before opening a
new phase.

## Task Notes

### 022_001 - Bulk CSV Export for Filtered Admin Lists

#### Step Goal

Allow an authorized Admin/User workspace caller to export all short links
matching the active discovery filters as a deterministic CSV while retaining
the backend's existing ownership/share authorization boundary.

#### Scope

In:

- Add a feature-scoped CSV serializer and browser download helper.
- Add an Export CSV action to the short-link discovery toolbar.
- Fetch all filtered pages through the existing authorized list API before
  downloading, with a clear busy and retryable failure state.
- Add focused serializer and URL/filter coverage without putting secrets in
  fixtures.
- Update README documentation.

Out:

- New export endpoint or alternate authorization rules.
- Exporting audit secrets, API keys, credentials, or session material.
- QR generation or rate-limit dashboards; those remain later tasks in Phase 022.

#### Acceptance Criteria

- Export uses the current search/status/sort filters exactly as the list view.
- All pages are fetched with the existing API contract and merged in server
  order without duplicates.
- CSV has a stable header and escaped cells for commas, quotes, and newlines.
- Export includes only safe short-link fields and respects server-scoped results.
- The UI exposes loading, success, and retryable failure feedback and does not
  block ordinary list discovery.
- Focused frontend tests and the production frontend build pass.

#### Foundation for Next Step

Leaves a reusable safe export utility and a tested filtered discovery boundary
for the QR presentation task and later operational visibility work.

### 022_002 - QR Code Presentation for Short Links

#### Step Goal

Give every authorized short link in the admin workspace a scannable QR
presentation that encodes only its existing public short URL and does not alter
redirect behavior or authorization boundaries.

#### Scope

In:

- Add a QR-code generation helper using the existing frontend dependency.
- Add a row action and accessible dialog for authorized short links.
- Show loading, retryable failure, URL context, and PNG download states.
- Add focused frontend coverage for safe payload and generated QR output.
- Update README and phase bookkeeping.

Out:

- New backend endpoint, persistence, or authorization rule.
- Encoding destination URLs, credentials, audit data, or session material.
- Bulk QR export or rate-limit visibility; those remain later tasks in Phase 022.

#### Acceptance Criteria

- An authorized short-link row exposes a QR action without changing existing actions.
- The QR payload is exactly the safe `shortUrl` returned by the authorized list API.
- The dialog has accessible labeling, loading/error recovery, URL context, and PNG download.
- Existing redirect, sharing, and list authorization behavior is unchanged.
- Focused frontend tests and the production frontend build pass.

#### Foundation for Next Step

Leaves a reusable QR presentation boundary for the later rate-limit visibility
task without adding another backend contract or exposing sensitive fields.

#### Affected Files

- `.okf/phase/022/PHASE_SUMMARY.md`
- `README.md`
- `src/ShortenLink.Web/package.json`
- `src/ShortenLink.Web/bun.lock`
- `src/ShortenLink.Web/src/features/short-links/qr.ts`
- `src/ShortenLink.Web/src/features/short-links/components/ShortLinkQrDialog.tsx`
- `src/ShortenLink.Web/src/features/short-links/pages/ShortLinkAdminPage.tsx`
- `src/ShortenLink.Web/src/styles.css`
- `src/ShortenLink.Web/test/short-link-qr.test.ts`

#### Verification

```powershell
Set-Location .\src\ShortenLink.Web
bun test
bun run build
Set-Location ..\..
dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal --disable-build-servers
```

#### Done Notes

- Added the `qrcode` frontend dependency and a feature-scoped helper that
  generates a PNG data URL from the authorized public `shortUrl`.
- Added an accessible `QR code` row action and dialog with loading, retryable
  error, URL confirmation, and `Download PNG` states.
- Kept the QR flow client-side and reused the existing authorized list result;
  no destination URL, credential, audit field, or session material is encoded.
- Documented the QR contract in `README.md`.
- Verification:
  - `bun test` passed: 51 frontend tests.
  - `bun run build` passed.
  - `dotnet build` passed with 0 warnings and 0 errors.
  - `dotnet test` passed: 7 Application, 46 Core, 39 Infrastructure, and 75
    API tests (167 total).

#### Affected Files

- `.okf/phase/022/PHASE_SUMMARY.md`
- `README.md`
- `src/ShortenLink.Web/src/features/short-links/export.ts`
- `src/ShortenLink.Web/src/features/short-links/components/ShortLinkDiscoveryToolbar.tsx`
- `src/ShortenLink.Web/src/features/short-links/pages/ShortLinkAdminPage.tsx`
- `src/ShortenLink.Web/test/short-link-export.test.ts`

#### Verification

```powershell
Set-Location .\src\ShortenLink.Web
bun test
bun run build
Set-Location ..\..
dotnet build ShortenLink.slnx --no-restore --verbosity minimal
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal
```

#### Done Notes

- Added a feature-scoped deterministic CSV serializer with safe short-link
  metadata only and correct escaping for commas, quotes, and newlines.
- Added an `Export CSV` toolbar action that reuses the active discovery filters,
  fetches every authorized page through the existing list API, deduplicates by
  stable code, and provides busy, success, and retryable failure feedback.
- Documented the export contract in `README.md` without adding a new endpoint or
  changing authorization behavior.
- Verification:
  - `bun test` passed: 48 frontend tests.
  - `bun run build` passed.
  - `dotnet build ShortenLink.slnx --no-restore --verbosity minimal` passed with
    0 warnings and 0 errors.
  - `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
    passed: 46 Core, 5 Application, 38 Infrastructure, and 75 API tests
    (164 total).

### 022_003 - Rate-Limit Visibility for the Admin Workspace

#### Step Goal

Allow Admin to inspect configured create/redirect rate-limit policies and a
bounded recent rejection history without exposing IP addresses, URLs, short-link
codes, request payloads, or credentials.

#### Scope

In:

- Record policy-level throttling activity in a bounded in-memory monitor.
- Add an Admin-only API response for configuration and recent safe events.
- Add a rate-limit visibility panel to the Admin Dashboard with recovery state.
- Add focused backend and frontend coverage plus README documentation.
- Preserve existing 429 behavior and avoid persistence or request-sensitive data.

Out:

- Per-user/IP analytics, raw request logging, durable rate-limit event storage,
  configuration mutation, or a new permission catalog entry.
- Changes to permit, queue, or window behavior.

#### Acceptance Criteria

- Existing create and redirect rate limits continue to return 429 as configured.
- Admin can query enabled state and create/redirect permit, window, and queue configuration.
- Recent throttles show policy, timestamp, and aggregate counts only, with a bounded memory footprint.
- Non-Admin callers cannot query the global rate-limit view.
- Dashboard loading, unavailable, disabled, enabled, and recent-throttle states are clear.
- Focused frontend/backend tests, production frontend build, backend build, and full backend tests pass.

#### Foundation for Next Step

Leaves a safe operational visibility contract that can later be backed by a
durable metrics system without exposing request-sensitive data in the UI/API.

#### Affected Files

- `.okf/phase/022/PHASE_SUMMARY.md`
- `README.md`
- `shared/ShortenLink.Hosting/ShortenLinkRateLimitMonitor.cs`
- `shared/ShortenLink.Hosting/ShortenLinkServiceCollectionExtensions.cs`
- `src/ShortenLink.Application/Abstractions/IRateLimitActivityReader.cs`
- `src/ShortenLink.Application/Contracts/Responses/RateLimitResponses.cs`
- `src/ShortenLink.Application/Features/RateLimiting/GetRateLimitActivityQuery.cs`
- `src/ShortenLink.Api/Endpoints/RateLimitEndpoints.cs`
- `src/ShortenLink.Api/Program.cs`
- `src/ShortenLink.Web/src/features/short-links/api/shortLinksApi.ts`
- `src/ShortenLink.Web/src/features/short-links/types.ts`
- `src/ShortenLink.Web/src/features/short-links/rateLimitPresentation.ts`
- `src/ShortenLink.Web/src/features/short-links/pages/AdminDashboardPage.tsx`
- `src/ShortenLink.Web/src/styles.css`
- `src/ShortenLink.Web/test/rate-limit-visibility.test.ts`
- `tests/ShortenLink.Api.Tests/ShortLinkEndpointsTests.cs`

#### Verification

```powershell
Set-Location .\src\ShortenLink.Web
bun test
bun run build
Set-Location ..\..
dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal --disable-build-servers
```

#### Done Notes

- Added a bounded process-local monitor hooked into ASP.NET rate-limiter
  rejection callbacks. It tracks only create/redirect policy counts and recent
  policy/timestamp pairs.
- Added Admin-only `GET /api/admin/rate-limits` for safe configuration and
  activity visibility; existing 429 behavior and policy settings are unchanged.
- Added dashboard loading, disabled, enabled, unavailable, aggregate, and
  recent-throttle states without displaying IPs, URLs, codes, payloads, or keys.
- Documented the endpoint's safe response and process-local retention model.
- Verification:
  - `bun test` passed: 53 frontend tests.
  - `bun run build` passed.
  - `dotnet build` passed with 0 warnings and 0 errors.
  - `dotnet test` passed: 7 Application, 46 Core, 39 Infrastructure, and 77
    API tests (169 total).

## Scan Rule

Reuse the existing list API and server authorization. Keep exports deterministic,
non-secret, and scoped to records returned by the backend.
