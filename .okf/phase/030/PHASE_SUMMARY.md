---
phase: 030
title: Backend Cohesion And Persistence Decomposition
status: active
created_at: 2026-08-07
updated_at: 2026-08-07
current_task: null
task_count: 2
done_count: 2
depends_on:
  - 029
---

# Phase 030 Summary

## Phase Goal

Improve backend cohesion by separating persistence read/query concerns from
mutation and provider lifecycle code while preserving public contracts,
runtime behavior, and dependency direction.

## Phase Done Criteria

- Persistence repositories expose stable facades with cohesive internal
  read/query and write boundaries.
- EF model configuration, messaging lifecycle, and session responsibilities
  are independently testable where extraction has a concrete ownership benefit.
- No route, payload, business rule, authorization policy, or persistence
  semantic changes are introduced.
- Full solution build and tests remain green after each task.

## Task Index

| Task | Title | Category | Status | Done At |
|---|---|---|---|---|
| 030_001 | Extract ShortLink repository read/query boundary | Refactor | done | 2026-08-07 |
| 030_002 | Extract EF entity mappings from ShortLinkDbContext | Refactor | done | 2026-08-07 |

## Current Task

`030_002` is complete. The ShortLink repository and EF persistence context now
have explicit read/write and model-configuration boundaries while preserving
the existing schema contract.

## Next Task Proposal

After review, consider `030_003` to separate RabbitMQ connection lifecycle,
publishing, and consuming responsibilities.

## Task Notes

### 030_001 - Extract ShortLink repository read/query boundary

#### Step Goal

Separate read/query and cursor/filter/sort responsibilities from ShortLink
mutation and provider-conflict handling without changing repository contracts.

#### Done Notes

- Isolated list, cursor, access-scope, filter/sort, count, and lookup methods
  into `EfCoreShortLinkRepository.Read.cs`.
- Preserved the existing repository facade, interfaces, provider conflict
  handling, projections, and query semantics.
- Build passed with 0 warnings and 0 errors.
- All 232 solution tests passed.

### 030_002 - Extract EF entity mappings from ShortLinkDbContext

#### Step Goal

Move EF entity/table/index/property mappings out of `ShortLinkDbContext` into
focused configuration classes without changing the generated model or schema.

#### Scope

- Add one `IEntityTypeConfiguration<T>` per persistence entity.
- Centralize shared base-entity mapping in a small internal helper.
- Keep conventions, DbSets, table names, indexes, conversions, and constraints
  unchanged.

#### Acceptance Criteria

- `ShortLinkDbContext.OnModelCreating` only applies the configuration assembly.
- All existing persistence mappings remain represented in configuration files.
- No migration, schema, repository, or runtime contract changes.
- Full solution verification passes.

#### Foundation for Next Step

EF persistence mappings are now independently owned and testable, leaving
messaging lifecycle decomposition as the next isolated backend boundary.

#### Affected Files

- `src/ShortenLink.Infrastructure/Persistence/ShortLinkDbContext.cs`
- `src/ShortenLink.Infrastructure/Persistence/Configurations/`
- `.okf/phase/030/PHASE_SUMMARY.md`

#### Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal
```

#### Done Notes

- Extracted all ten persistence entity mappings into focused configuration
  classes and centralized shared base-entity mapping.
- Preserved table names, indexes, constraints, conversions, and DbSets.
- Build passed with 0 warnings and 0 errors.
- All 232 solution tests passed.
