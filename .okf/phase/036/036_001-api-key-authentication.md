---
task: 036_001
phase: 036
title: API Key Authentication Consumer Contract
status: done
created_at: 2026-08-09
depends_on:
  - 035_001
completed_at: 2026-08-09
---

# 036_001 - API Key Authentication Consumer Contract

## Step Goal

Let external applications call permission-protected ShortenLink API endpoints
with an API key and no browser session, using a stable standard scheme while
keeping existing configured-header clients compatible.

## Scope

In:

- Accept `Authorization: ApiKey <key>` and the standard `X-Api-Key` header.
- Preserve the configured `ShortenLink:Security:HeaderName` header.
- Allow security-enabled hosts to use persisted user-owned API keys without a
  configured bootstrap key.
- Document the request contract and verify success, invalid, disabled, and
  permission-denied behavior.

Out:

- API-key lifecycle changes, expiration, rotation, per-key scopes, OAuth/OIDC,
  or session-authenticated key-management changes.

## Acceptance Criteria

- A configured or user-owned API key authorizes a protected endpoint without a
  `Bearer` session token through `Authorization: ApiKey <key>`.
- `X-Api-Key` is accepted as a portable header alias while the configured
  header remains supported.
- Missing/invalid/disabled keys return the existing generic 401 response, and
  insufficient permissions return the existing generic 403 response.
- Security-enabled configuration with no configured bootstrap keys remains
  valid for persisted-key hosts.
- README and focused API tests describe and verify the supported contract.

## Foundation for Next Step

This leaves a stable, session-independent authentication boundary for external
NuGet/API consumers; subsequent roadmap work can add analytics without
coupling it to a browser login flow.

## Affected Files

- `shared\ShortenLink.Hosting\Security\Authorization.cs`
- `shared\ShortenLink.Hosting\Options\ShortenLink.cs`
- `shared\ShortenLink.Hosting\Registration\Services.cs`
- `shared\ShortenLink.Hosting\Registration\Validation.cs`
- `tests\ShortenLink.Api.Tests\ShortLinkEndpointsTests.cs`
- `README.md`
- `.okf\phase\036\PHASE_SUMMARY.md`

## Verification

```powershell
dotnet build ShortenLink.slnx --no-restore
dotnet test ShortenLink.slnx --no-build --no-restore
git diff --check
```

## Done Notes

- Added `Authorization: ApiKey <key>` and `X-Api-Key: <key>` extraction for
  permission-protected endpoints while preserving the configured API-key
  header.
- Relaxed security configuration validation so persisted user-owned keys can
  be the only runtime credentials; configured keys remain optional bootstrap
  fallback credentials.
- Updated README with the portable client contract and a no-session curl
  example.
- Added API regression coverage for the standard scheme, standard header,
  user-owned key authorization, disabled-key rejection, and keyless security
  configuration.
- Verification passed: `dotnet build ShortenLink.slnx --no-restore`,
  `dotnet test ShortenLink.slnx --no-build --no-restore` (247 tests), and
  `git diff --check`.
