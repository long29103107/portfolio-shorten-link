---
phase: 036
title: API Key Authentication
status: complete
created_at: 2026-08-09
updated_at: 2026-08-09
current_task: null
task_count: 1
done_count: 1
depends_on:
  - 035
---

# Phase 036 Summary

## Phase Goal

Provide a documented, portable API-key authentication contract so external
applications can call permission-protected ShortenLink APIs without creating a
browser session, while preserving hash-only storage and existing session,
configured-key, and persisted-assignment compatibility.

## Phase Done Criteria

- API clients can authenticate with a user-owned or configured API key without
  a session token through a stable API-key scheme.
- Existing configured API-key header behavior remains compatible, with a
  standard header/scheme available to NuGet/API consumers.
- Missing, invalid, disabled, and under-permissioned API keys retain the
  existing generic 401/403 contract.
- Security configuration can rely on persisted user-owned keys without
  requiring a duplicate bootstrap key in configuration.
- README and API regression tests document and verify the client contract.

## Scope

In:

- Standard API-key request schemes for protected endpoints.
- Configuration validation needed for persisted user-owned API-key-only hosts.
- API-key authentication documentation and regression coverage.

Out:

- OAuth/OIDC, API-key expiration, rotation reminders, per-key permission
  narrowing, rate limits per credential, and API-key management UI changes.

## Task Index

| Task | Title | Category | Status | Done At |
|---|---|---|---|---|
| 036_001 | API Key Authentication Consumer Contract | Feature | done | 2026-08-09 |

## Current Task

`036_001` is complete. The stable API-key request contract is available through
standard headers/scheme and the existing configured header remains compatible.

## Next Task Proposal

Phase 036 is complete. The next roadmap capability is Advanced Analytics:
device, browser, OS, referrer, country, and unique-click reporting.

## Task Notes

See [036_001-api-key-authentication.md](036_001-api-key-authentication.md).

### 036_001 - API Key Authentication Consumer Contract

- Added portable `Authorization: ApiKey <key>` and `X-Api-Key: <key>` request
  forms while preserving the configured `ShortenLink:Security:HeaderName`
  header.
- Allowed security-enabled hosts to omit configured bootstrap keys when they
  rely on persisted user-owned API keys.
- Verified configured and user-owned key access without session tokens,
  disabled-key rejection, existing 401/403 behavior, and API documentation.
- Verification passed: solution build, full backend test suite (247 tests),
  and `git diff --check`.
