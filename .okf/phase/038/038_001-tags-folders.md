---
phase: 038
task: 038_001
title: Tags / Folders
status: done
created_at: 2026-08-09
started_at: 2026-08-09
completed_at: 2026-08-09
depends_on:
  - 037_001
---

# 038_001 - Tags / Folders

## Step Goal

Make short links easier to organize by adding flat folders and reusable tags
to the existing ownership-aware management flow.

## Scope

- Add one flat folder label and zero or more normalized tag labels to each
  short link.
- Expose folder/tag data through create, update, detail, list, and export
  contracts where those contracts already expose link metadata.
- Add folder/tag discovery filters; taxonomy CRUD remains out of scope for this
  first organization slice.
- Add compact create/edit controls and management filters in the React app.
- Preserve Admin bypass and User owner/share authorization semantics.

## Acceptance Criteria

- A link can be created or updated with one folder label and a list of tag names;
  duplicate tag names normalize to one assignment.
- Folder and tag names are trimmed, bounded, and normalized case-insensitively;
  blank values remain valid and duplicate tags collapse per link.
- Empty organization metadata remains valid and old links are readable.
- `GET /api/short-links` can filter by folder or tag without changing existing
  search/status/sort behavior.
- The React management UI can create/edit organization metadata and visibly
  filter the list.
- Backend/API/frontend tests and builds pass, and README/API contracts are
  updated.

## Foundation for Next Step

The task leaves normalized folder/tag metadata and discovery filters that a
later bulk-operations task can reuse.

## Affected Files

- `src/ShortenLink.Core/Domain`
- `src/ShortenLink.Core/Contracts`
- `src/ShortenLink.Application/Features/ShortLinks`
- `src/ShortenLink.Infrastructure/Persistence`
- `shared/ShortenLink.Hosting/Endpoints`
- `src/ShortenLink.Web/src/features/short-links`
- `tests/ShortenLink.*.Tests`
- `README.md`

## Verification

- `dotnet build ShortenLink.slnx --no-restore`
- `dotnet test ShortenLink.slnx --no-build --no-restore`
- `cd src/ShortenLink.Web; bun test`
- `cd src/ShortenLink.Web; bun run build`
- `git diff --check`

## Done Notes

Implemented flat folder metadata and normalized reusable tags across the domain,
SQLite/PostgreSQL compatibility schema, EF persistence, create/update/detail/list
and export contracts, plus folder/tag list filtering. The React create/edit
forms, detail/result views, admin table, CSV export, and discovery toolbar now
expose the same metadata. Invalid folder/tag errors map to their controls, and
old links remain valid with empty metadata.

Verification passed:

- `dotnet build ShortenLink.slnx --no-restore`
- `dotnet test ShortenLink.slnx --no-build --no-restore` (256 tests)
- `bun test` (84 tests)
- `bun run build`
- `git diff --check` (exit code 0; only Git line-ending warnings)
