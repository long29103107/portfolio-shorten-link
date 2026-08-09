---
phase: 038
title: Tags and Folders
status: complete
created_at: 2026-08-09
updated_at: 2026-08-09
current_task: null
task_count: 1
done_count: 1
depends_on:
  - 037
---

# Phase 038 Summary

## Phase Goal

Give signed-in users a compact way to organize owned and shared short links
with a flat folder label and reusable normalized tags, then discover links
through the same management API and UI.

## Phase Done Criteria

- Links can be assigned to at most one folder and any number of normalized
  tags without breaking existing create/update/authorization behavior.
- Folder and tag labels are managed as link metadata, so old links can remain
  unassigned without requiring a separate taxonomy lifecycle.
- Link list/detail/create/edit contracts expose folder and tags, and list
  filtering can target folder or tag.
- SQLite/PostgreSQL schema, API, React admin UI, tests, and docs are verified.

## Scope

In:

- Flat folder labels.
- Reusable tag labels with case-insensitive normalized names.
- Link folder/tag metadata.
- Link create/update/list APIs and management UI integration.
- Folder/tag filtering in short-link discovery.

Out:

- Separate folder/tag CRUD, nested folders, folder sharing, tag colors, tag
  hierarchy, global taxonomy, and bulk tag/folder operations.

## Task Index

| Task | Title | Category | Status | Done At |
|---|---|---|---|---|
| 038_001 | Tags / Folders | Feature | done | 2026-08-09 |

## Current Task

`038_001` is complete. It establishes the persistence, API, filtering, and UI
foundation for organizing links by folder and reusable tags.

## Next Task Proposal

After verification, evaluate whether bulk operations should accept folder/tag
assignment in a follow-up task.

## Task Notes

See [038_001-tags-folders.md](038_001-tags-folders.md).
