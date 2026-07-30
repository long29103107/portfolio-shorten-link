---
task: 025_005
phase: 025
title: MyBlog base contract migration
status: done
created_at: 2026-07-30
updated_at: 2026-07-30
completed_at: 2026-07-30
---

# 025_005 - MyBlog Base Contract Migration

## Step Goal

Adopt the reviewed MyBlog request/list/paging/response shape as framework-free
Core contracts and use grouped request binding for multi-parameter endpoints.

## Scope

- Production `Request`, `ListRequest`, `PagingListRequest`, `Response`,
  `ListResponse<T>`, and `PagingListResponse<T>` contracts.
- Preserve `fe`, `sort`, count, page, page-size, and navigation metadata.
- Keep existing response JSON contracts stable.

## Acceptance Criteria

- Contracts compile without ASP.NET MVC or Newtonsoft.Json dependencies.
- Existing endpoint/request/response contracts remain unchanged.
- Endpoint binding remains optional for legacy paging inputs.

## Foundation for Next Step

The API now has a reusable base contract foundation for migrating additional
multi-parameter endpoints without changing existing JSON contracts.

## Affected Files

- `src/ShortenLink.Core/Contracts/Requests/`
- `src/ShortenLink.Core/Contracts/Responses/`
- `shared/ShortenLink.Hosting/ShortLinkListEndpointRequest.cs`
- `src/ShortenLink.Api/Endpoints/AuditLogEndpointRequest.cs`
- `.okf/phase/025/PHASE_SUMMARY.md`

## Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal
```

## Done Notes

- Added production framework-free base request/list/paging/response contracts
  using `System.Text.Json` only.
- Applied grouped request binding to short-link and audit-log list endpoints.
- Added contract tests for paging defaults, sort metadata, JSON stability, and
  endpoint request defaults.
- Removed the temporary prototype duplicates.
- Verification: solution build passed with 0 warnings and 0 errors; all 173
  tests passed.
