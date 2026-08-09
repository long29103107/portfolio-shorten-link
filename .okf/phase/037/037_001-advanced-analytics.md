---
task: 037_001
phase: 037
title: Advanced Analytics
status: done
created_at: 2026-08-09
depends_on:
  - 036_001
completed_at: 2026-08-09
---

# 037_001 - Advanced Analytics

## Step Goal

Capture and expose useful click dimensions—device, browser, operating system,
referrer, country, and unique visitors—through the existing short-link
analytics API and admin dialog without storing or returning a raw visitor
fingerprint.

## Scope

In:

- Classify device, browser, and operating system from the user-agent.
- Read a country code from the configured/trusted proxy country header, with
  `CF-IPCountry` and `X-Country-Code` compatibility.
- Store a SHA-256 visitor key derived from IP + user-agent and aggregate unique
  clicks by distinct non-null key.
- Extend click contracts, EF persistence, SQLite/PostgreSQL compatibility
  schema, analytics aggregation, API response, frontend presentation, docs,
  and regression tests.

Out:

- GeoIP service/database integration, raw visitor-key exposure, authenticated
  visitor identity, click export, and a separate dashboard route.

## Acceptance Criteria

- Successful redirect analytics persist normalized metadata and old click
  recording modes continue to work.
- `GET /api/short-links/{code}/analytics` returns total clicks, nullable
  unique-click count, last click, enriched recent clicks, and bounded
  device/browser/OS/referrer/country breakdowns.
- Unique clicks count distinct IP + user-agent fingerprints and do not expose
  the fingerprint in API responses or frontend state.
- Country input is normalized and only documented as trusted when supplied by
  a proxy; absent country remains null.
- Existing databases gain the new nullable columns idempotently for SQLite and
  PostgreSQL, and tenant analytics remain isolated.
- API/frontend tests, builds, and full backend verification pass.

## Foundation for Next Step

This leaves a stable enriched click model and bounded analytics aggregation for
future tags/folders, bulk operations, webhooks, and telemetry work.

## Affected Files

- `src\ShortenLink.Core\Domain\ShortLinkClickEntity.cs`
- `src\ShortenLink.Core\Contracts\Requests\RecordShortLinkClickRequest.cs`
- `src\ShortenLink.Core\Contracts\Responses\ShortLinkClickAnalyticsSummary.cs`
- `src\ShortenLink.Core\Abstractions\DataAccess\IShortLinkClickRepository.cs`
- `src\ShortenLink.Infrastructure\Persistence\`
- `src\ShortenLink.Infrastructure\Repositories\EfCoreShortLinkClickRepository.cs`
- `src\ShortenLink.Application\Contracts\Responses\ApplicationResponses.cs`
- `src\ShortenLink.Application\Features\ShortLinks\Analytics\GetShortLinkAnalyticsQuery.cs`
- `shared\ShortenLink.Hosting\Endpoints\Map.cs`
- `shared\ShortenLink.Hosting\Messaging\`
- `src\ShortenLink.Web\src\features\short-links\`
- `README.md`
- related Core/Infrastructure/API/Web tests

## Verification

```powershell
dotnet build ShortenLink.slnx --no-restore
dotnet test ShortenLink.slnx --no-build --no-restore
cd .\src\ShortenLink.Web
bun test
bun run build
git diff --check
```

## Done Notes

- Added normalized device, browser, operating-system, and country metadata to
  successful click records.
- Added a SHA-256 IP + user-agent visitor key for distinct unique-click
  aggregation; the key is persisted only for counting and is excluded from
  API/frontend contracts.
- Added optional advanced click repository contracts, EF Core SQLite/Postgres
  aggregation, tenant filtering, indexes, and idempotent compatibility schema
  upgrades.
- Extended the existing analytics API and admin dialog with unique visitors,
  dimension breakdowns, and enriched recent activity.
- Documented the trusted `CF-IPCountry` / `X-Country-Code` input contract and
  no-GeoIP behavior.
- Verification passed: `dotnet build ShortenLink.slnx --no-restore`,
  `dotnet test ShortenLink.slnx --no-build --no-restore` (251 tests),
  `bun test` (83 tests), `bun run build`, and `git diff --check`.
