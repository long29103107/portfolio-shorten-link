---
phase: 030
title: Backend Cohesion And Persistence Decomposition
status: complete
created_at: 2026-08-07
updated_at: 2026-08-07
current_task: null
task_count: 5
done_count: 5
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
| 030_003 | Separate RabbitMQ lifecycle, publisher, and consumer | Refactor | done | 2026-08-07 |
| 030_004 | Separate session token, principal, and session-state responsibilities | Refactor | done | 2026-08-07 |
| 030_005 | Audit dependency direction and remaining structural duplication | Architecture | done | 2026-08-07 |

## Current Task

`030_005` is complete. Phase-wide dependency direction and remaining
duplication were audited; the graph remains acyclic and no additional shared
package or behavior-neutral extraction is justified.

## Next Task Proposal

Phase 030 is complete. Select the next phase from the product roadmap rather
than adding more refactor-only tasks here.

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

### 030_005 - Audit dependency direction and remaining structural duplication

#### Step Goal

Verify the phase-wide dependency graph and remove only structural duplication
that has a measurable ownership or reuse benefit.

#### Scope

- Audit project references for cycles and forbidden inward dependencies.
- Review remaining similarly-shaped options, serializer helpers, and contract
  candidates against the reuse boundary rule.
- Record decisions and guardrails without introducing a new package or
  behavior-neutral abstraction without a legitimate consumer.

#### Acceptance Criteria

- Dependency graph is documented and acyclic.
- Remaining duplication has an explicit ownership decision.
- No unnecessary shared package or coupling is introduced.
- Full solution verification passes.

#### Foundation for Next Step

Phase 030 leaves verified persistence, messaging, session, and dependency
boundaries. The next phase can focus on product roadmap work without carrying
unresolved backend structural debt.

#### Affected Files

- `.okf/decisions/030-005-dependency-audit.md`
- `.okf/phase/030/PHASE_SUMMARY.md`

#### Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal
```

#### Done Notes

- Verified the project-reference graph is acyclic and preserves layer
  direction.
- Documented why remaining queue options and serializer helpers should not be
  merged across ownership boundaries.
- Build passed with 0 warnings and 0 errors.
- All 232 solution tests passed.

### 030_003 - Separate RabbitMQ lifecycle, publisher, and consumer

#### Step Goal

Split RabbitMQ connection lifecycle, publishing, and consuming responsibilities
without changing `IMessageQueue<T>`, delivery acknowledgement, cancellation, or
provider selection behavior.

#### Scope

- Keep `RabbitMqMessageQueue<T>` as the public queue façade.
- Isolate connection/queue declaration/disposal, publisher, and consumer code
  into focused partial implementation files.
- Preserve persistent publishing, manual ack/nack, bounded delivery channel,
  recovery settings, and cancellation boundaries.

#### Acceptance Criteria

- RabbitMQ code is separated into connection, publisher, and consumer files.
- Existing queue contracts and message envelope behavior remain unchanged.
- No broker, retry, acknowledgement, or configuration semantics change.
- Full solution verification passes.

#### Foundation for Next Step

Messaging responsibilities now have explicit source boundaries, leaving the
session/security lifecycle as the next isolated backend cohesion task.

#### Affected Files

- `shared/ShortenLink.Messaging/RabbitMq/RabbitMqMessageQueue.cs`
- `shared/ShortenLink.Messaging/RabbitMq/RabbitMqMessageQueue.Connection.cs`
- `shared/ShortenLink.Messaging/RabbitMq/RabbitMqMessageQueue.Publisher.cs`
- `shared/ShortenLink.Messaging/RabbitMq/RabbitMqMessageQueue.Consumer.cs`
- `.okf/phase/030/PHASE_SUMMARY.md`

#### Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal
```

#### Done Notes

- Isolated RabbitMQ connection/queue lifecycle and disposal, publisher setup
  and publish flow, and consumer setup/delivery/stop flow.
- Preserved publisher confirms, manual acknowledgement, requeue behavior,
  bounded delivery channel, automatic recovery, and cancellation semantics.
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

### 030_004 - Separate session token, principal, and session-state responsibilities

#### Step Goal

Split session token cryptography, principal/permission projection, and HTTP
session-state handling without changing login, refresh, or current-user
behavior.

#### Scope

- Keep `UserSessionService` as the existing public service facade.
- Isolate principal and permission projection, token signing/validation, and
  bearer/session-state handling into focused partial implementation files.
- Preserve token payload shape, signing key derivation, TTL checks, and error
  results.

#### Acceptance Criteria

- Session responsibilities are separated into principal, token, and session
  state files.
- Existing session interfaces, token behavior, authorization integration, and
  error contracts remain unchanged.
- No authentication algorithm or policy changes are introduced.
- Full solution verification passes.

#### Foundation for Next Step

Security session concerns now have explicit source boundaries, allowing a
phase-wide dependency audit without mixing it with behavior changes.

#### Affected Files

- `shared/ShortenLink.Hosting/Security/Session.cs`
- `shared/ShortenLink.Hosting/Security/UserSessionService.Principal.cs`
- `shared/ShortenLink.Hosting/Security/UserSessionService.Token.cs`
- `shared/ShortenLink.Hosting/Security/UserSessionService.SessionState.cs`
- `.okf/phase/030/PHASE_SUMMARY.md`

#### Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal --disable-build-servers
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal
```

#### Done Notes

- Isolated permission/principal projection, token signing/validation, and
  bearer/current-user session handling into focused partial files.
- Preserved token payload, TTL, signing-key derivation, login/refresh behavior,
  authorization integration, and error contracts.
- Build passed with 0 warnings and 0 errors.
- All 232 solution tests passed.
