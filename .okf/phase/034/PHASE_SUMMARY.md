---
phase: 034
title: Short-Link Lifecycle Scheduling
status: complete
created_at: 2026-08-09
updated_at: 2026-08-09
current_task: null
task_count: 1
done_count: 1
depends_on:
  - 033
---

# Phase 034 Summary

## Phase Goal

Add explicit short-link lifecycle scheduling so links can become available at
a configured future time while continuing to honor their expiration and manual
active/inactive state across the reusable library, API, persistence providers,
and React workspace.

## Phase Done Criteria

- `ActiveFrom` is represented in domain, persistence, and public create/update
  contracts with backward-compatible immediate activation when omitted.
- Redirect resolution allows a link only during the window from `ActiveFrom`
  through `ExpiresAt`, while preserving manual deactivation and expiry behavior.
- Create, update, import, list/detail/export responses, and the frontend editor
  expose the scheduled activation value consistently.
- Existing SQLite compatibility databases receive the new column without
  losing existing links or changing provider selection behavior.
- Backend tests, frontend tests/build, and relevant API verification pass.

## Scope

In:

- Scheduled activation with optional `ActiveFrom` and required future
  `ExpiresAt`.
- Validation that `ActiveFrom` is earlier than `ExpiresAt` when supplied.
- API, persistence, import/export, and frontend lifecycle presentation.

Out:

- Click limits, password protection, custom domains, or automatic background
  activation jobs.
- Changes to random Base62 code generation or authentication policy.

## Task Index

| Task | Title | Category | Status | Done At |
|---|---|---|---|---|
| 034_001 | Scheduled Activation | Feature | done | 2026-08-09 |

## Current Task

`034_001` is complete. `ActiveFrom` is persisted and exposed across the
scheduled activation window without breaking current immediate-activation
callers.

## Next Task Proposal

After `034_001`, propose `034_002 – Click Limit` to extend the same lifecycle
boundary with a concurrency-safe redirect count limit. Do not create it until
the current task is complete.

## Task Notes

Historical and active task detail is compacted here so the phase remains in one
file.

### 034_001 - Scheduled Activation

#### Step Goal

Add optional `ActiveFrom` to short links, reject invalid activation/expiry
windows, persist and expose it through all create/update/read paths, and block
redirects before the scheduled time while allowing them afterward until
expiration.

#### Scope

In:

- Core domain and service request contracts.
- EF Core entity/read model/schema compatibility for SQLite and PostgreSQL.
- Application commands, validation, API DTOs/responses, import/export.
- React create/edit fields and scheduled lifecycle presentation.
- Unit, integration, and frontend regression coverage.

Out:

- Background workers that mutate `IsActive` at the scheduled time.
- Click limits or other future roadmap items.

#### Acceptance Criteria

- Omitting `ActiveFrom` preserves current immediate activation behavior.
- `ActiveFrom >= ExpiresAt` is rejected with a field-mappable validation error.
- A future-scheduled link cannot redirect before `ActiveFrom`, redirects after
  `ActiveFrom`, and stops after `ExpiresAt`.
- Manual deactivate/activate and cache behavior remain correct.
- Existing databases upgrade idempotently and preserve existing rows.
- Backend solution tests/build and frontend tests/build pass.

#### Foundation for Next Step

The lifecycle model has an explicit activation window that `034_002` can extend
with a concurrency-safe `MaxClicks` limit without redefining expiry semantics.

#### Affected Files

- `src/ShortenLink.Core/Domain/ShortLinkEntity.cs`
- `src/ShortenLink.Core/Contracts/Requests/*`
- `src/ShortenLink.Core/Contracts/Responses/*`
- `src/ShortenLink.Infrastructure/Persistence/*`
- `src/ShortenLink.Application/*`
- `shared/ShortenLink.Hosting/Endpoints/Map.cs`
- `src/ShortenLink.Web/src/features/short-links/*`
- `tests/*`
- `.okf/phase/034/PHASE_SUMMARY.md`

#### Verification

```powershell
dotnet build ShortenLink.slnx
dotnet test ShortenLink.slnx
cd .\src\ShortenLink.Web
bun test
bun run build
```

#### Done Notes

- Added optional `ActiveFrom` to the domain, create/update/import requests,
  persistence entities/read models, API responses, export records, and the
  React create/edit flows.
- Enforced `ActiveFrom < ExpiresAt` and added field-mappable
  `invalid_activation_window` errors.
- Redirects now return `scheduled` before activation, preserve manual inactive
  and expired behavior, and invalidate scheduled cache entries.
- Added idempotent SQLite/PostgreSQL compatibility schema handling and an
  `ActiveFrom` index.
- Added scheduled lifecycle presentation, status filtering, CSV export, and
  regression coverage for domain, persistence, API, and frontend behavior.
- Verification passed: `dotnet build ShortenLink.slnx --no-restore`, full
  backend `dotnet test ShortenLink.slnx --no-build --no-restore` (235 tests),
  `bun test` (80 tests), and `bun run build`.
