---
task: 025_005
phase: 025
title: MyBlog base contract prototype review
status: active
created_at: 2026-07-30
updated_at: 2026-07-30
---

# 025_005 - MyBlog Base Contract Prototype Review

## Step Goal

Clone the MyBlog request/list/paging/response base shape into a framework-free
Core prototype so the contract can be reviewed before integration.

## Scope

- Prototype `Request`, `ListRequest`, `PagingListRequest`, `Response`,
  `ListResponse<T>`, and `PagingListResponse<T>`.
- Preserve `fe`, `sort`, count, page, page-size, and navigation metadata.
- Keep the prototype isolated from existing production DTOs.

## Acceptance Criteria

- Prototype compiles without ASP.NET MVC or Newtonsoft.Json dependencies.
- Existing endpoint/request/response contracts remain unchanged.
- The differences from MyBlog are documented for review.

## Foundation for Next Step

After review, a follow-up task can selectively migrate compatible list DTOs
without changing existing API JSON contracts.

## Affected Files

- `src/ShortenLink.Core/Contracts/Prototype/`
- `.okf/phase/025/PHASE_SUMMARY.md`

## Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal
```

## Done Notes

Not implemented yet.
