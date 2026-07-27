---
phase: 020
title: Clean Architecture, DDD, and Mediator alignment
status: complete
created_at: 2026-07-26
updated_at: 2026-07-26
current_task: null
task_count: 8
done_count: 8
depends_on:
  - 019
---

# Phase 020 Summary

## Phase Goal

Align the backend with a maintainable Clean Architecture and DDD boundary:
API endpoints remain thin transport adapters, Application owns use-case
orchestration through the custom Mediator, Core owns business contracts and
invariants, and Infrastructure owns persistence details.

## Phase Done Criteria

- Every business endpoint dispatches a request through the custom Mediator;
  only operational health checks may remain direct.
- Persistence-only EF entities and mappings are outside Core/Domain.
- HTTP response DTOs are outside Core; Application contracts are transport
  agnostic and named as requests, queries, or results.
- Use-case orchestration is in Application handlers, while Core services and
  entities contain business rules and invariants.
- Cross-repository mutations have an explicit transaction/unit-of-work
  boundary and do not leave partial state on failure.
- Identity/time/secret generation has injectable abstractions where it affects
  business behavior or test determinism.
- The Admin/User boundary is represented consistently; per-link View/Edit is
  access data, not a system-role alias.
- Mediator pipeline behaviors cover the shared validation, logging, and
  transaction concerns needed by the application.
- Backend build and focused tests pass with no new warnings.

## Scope

In:

- API endpoint-to-Mediator migration and endpoint thinness.
- Core/Application/Infrastructure dependency and model placement cleanup.
- Transaction, identity, time, and Mediator pipeline seams needed by use cases.
- Documentation and phase bookkeeping for the architecture migration.

Out:

- New product features unrelated to architecture.
- Frontend redesign.
- Replacing the custom Mediator with a third-party package.
- Destructive database reset unless a schema move requires it and is explicitly
  verified.

## Task Index

| Task | Title | Status | Done At |
|---|---|---|---|
| 020_001 | Route mock-data business operations through Mediator | done | 2026-07-26 |
| 020_002 | Move persistence entities and EF mappings out of Core | done | 2026-07-26 |
| 020_003 | Separate API responses from Core contracts | done | 2026-07-26 |
| 020_004 | Move short-link orchestration into Application handlers | done | 2026-07-26 |
| 020_005 | Add explicit unit-of-work boundaries for multi-write use cases | done | 2026-07-26 |
| 020_006 | Add injectable identity, time, and secret-generation seams | done | 2026-07-26 |
| 020_007 | Normalize Admin/User roles and per-link access levels | done | 2026-07-26 |
| 020_008 | Add Mediator pipeline behaviors and close architecture verification | done | 2026-07-26 |

## Current Task

No task is active. All eight architecture alignment tasks are complete and
verified.

## Completed Notes

- `020_001` moved mock seed orchestration, current-user authorization, URL
  generation, and service calls into the Application Mediator handler. The API
  endpoint now only binds the count, dispatches `ISender`, and returns the
  result.
- `020_002` moved all EF-only `*PersistenceEntity` types into
  `Infrastructure/Persistence/Entities`; Core/Domain now contains business
  entities only.
- `020_003` moved application/API response contracts out of Core, including
  health and mock-seed responses.
- `020_004` moved `ShortLinkService` orchestration into Application while Core
  retains the service contract and domain rules.
- `020_005` added `IUnitOfWork`, EF transaction execution, and a Mediator
  transaction behavior for atomic multi-write requests.
- `020_006` added injectable secure-token generation and removed direct
  random-secret generation from the API-key handler; login fallback time now
  uses `TimeProvider`.
- `020_007` kept only Admin/User as effective system-role bundles and retained
  legacy aliases only for source compatibility; View/Edit remains link-share
  access data.
- `020_008` added Mediator validation, logging, and unit-of-work pipeline
  behaviors with explicit open-generic registration.

## Next Task Proposal

Phase 020 is complete. The next phase is Phase 021, which can continue audit
producers on the cleaned architecture boundary.

## Task Notes

### 020_001 - Route Mock-Data Business Operations Through Mediator

#### Step Goal

Make the development mock-data endpoint a thin adapter by moving its seed
operation into an Application request/handler dispatched through `ISender`.

#### Scope

In:

- Add one Application command containing the mock seed input and result.
- Move authentication/session lookup, seed-loop orchestration, and service
  calls into the handler using existing abstractions.
- Keep `MockDataEndpoints` responsible only for binding, dispatch, and HTTP
  response formatting.
- Preserve the existing development-only authorization and response contract.

Out:

- Changing production short-link behavior.
- Adding a new mock-data feature beyond the existing endpoint.

#### Acceptance Criteria

- `MockDataEndpoints` injects `ISender` rather than `IShortLinkService`.
- The endpoint contains no business loop, current-user lookup, or persistence
  calls.
- The new handler path is covered by the focused mock-seed API regression test.
- Existing API tests and the backend build pass.

#### Foundation for Next Step

All business endpoints use the same Application/Mediator boundary, so the next
task can move persistence types without preserving an API-side business escape
hatch.

#### Affected Files

- `src/ShortenLink.Api/Endpoints/MockDataEndpoints.cs`
- `src/ShortenLink.Application/Features/`
- `tests/ShortenLink.Application.Tests/`
- `tests/ShortenLink.Api.Tests/`

#### Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal
```

#### Done Notes

- Added `SeedMockShortLinksCommand` and `SeedMockShortLinksCommandHandler`.
- Preserved anonymous development seeding and permission enforcement for an
  authenticated caller.
- `MockDataEndpoints` no longer injects `IShortLinkService` or the HTTP session
  service and contains no business loop.
- Verification:
  - `dotnet build ShortenLink.slnx --no-restore --verbosity minimal` passed with
    0 warnings and 0 errors.
- `dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal`
    passed: 45 Core, 4 Application, 37 Infrastructure, and 74 API tests.

### 020_002 - Move Persistence Entities and EF Mappings Out of Core

#### Step Goal

Keep EF-only persistence shapes in Infrastructure while preserving repository
contracts in Core.

#### Scope

In: move all `*PersistenceEntity` files and update mappings, repositories, and
tests. Out: changing the database model.

#### Acceptance Criteria

- Core/Domain has no persistence-only EF entity.
- Infrastructure owns persistence entities and the solution builds.

#### Foundation for Next Step

Core no longer depends on persistence representation details.

#### Affected Files

- `src/ShortenLink.Infrastructure/Persistence/Entities/`
- `src/ShortenLink.Infrastructure/Persistence/ShortLinkDbContext.cs`

#### Verification

Covered by the solution build and Infrastructure tests.

#### Done Notes

Completed 2026-07-26; all nine persistence entity types were moved and test
references updated.

### 020_003 - Separate API Responses From Core Contracts

#### Step Goal

Keep transport response DTOs in Application/API instead of Core.

#### Scope

In: move application response contracts and API-only health/mock responses.
Out: changing JSON field names.

#### Acceptance Criteria

- Response models compile from Application/API namespaces.
- Existing endpoint JSON contracts remain unchanged.

#### Foundation for Next Step

Use-case contracts can evolve without coupling Core to HTTP presentation.

#### Affected Files

- `src/ShortenLink.Application/Contracts/Responses/`
- `src/ShortenLink.Api/Responses/`

#### Verification

Covered by the solution build and API tests.

#### Done Notes

Completed 2026-07-26; response models moved without JSON contract changes.

### 020_004 - Move Short-Link Orchestration Into Application Handlers

#### Step Goal

Place short-link orchestration beside Application use cases while keeping the
Core service interface and domain rules stable.

#### Scope

In: move `ShortLinkService` implementation and registration dependency.
Out: changing short-link behavior.

#### Acceptance Criteria

- Application owns the orchestration implementation.
- API behavior and Core domain tests remain green.

#### Foundation for Next Step

Transaction behavior can wrap Application orchestration consistently.

#### Affected Files

- `src/ShortenLink.Application/Services/ShortLinkService.cs`
- `src/ShortenLink.AspNetCore/ShortenLinkServiceCollectionExtensions.cs`

#### Verification

Covered by Core/Application/API tests.

#### Done Notes

Completed 2026-07-26; service implementation moved to Application.

### 020_005 - Add Explicit Unit-of-Work Boundaries

#### Step Goal

Ensure a mediated use case with multiple writes commits or rolls back as one
transaction.

#### Scope

In: Core unit-of-work contract, EF implementation, and Mediator transaction
behavior. Out: changing individual repository APIs.

#### Acceptance Criteria

- An EF transaction wraps each mediated request.
- Existing persistence and API tests pass.

#### Foundation for Next Step

Mutation handlers have a durable transaction seam.

#### Affected Files

- `src/ShortenLink.Core/Abstractions/IUnitOfWork.cs`
- `src/ShortenLink.Infrastructure/Persistence/EfCoreUnitOfWork.cs`
- `src/ShortenLink.Application/Behaviors/UnitOfWorkPipelineBehavior.cs`

#### Verification

Covered by Infrastructure and API tests.

#### Done Notes

Completed 2026-07-26; EF transaction behavior is registered as an open generic.

### 020_006 - Add Injectable Identity, Time, and Secret Seams

#### Step Goal

Remove hard-coded random secret generation and wall-clock fallback from use
case handlers.

#### Scope

In: secure-token abstraction/implementation and `TimeProvider` injection.
Out: changing token formats or authentication semantics.

#### Acceptance Criteria

- API-key secrets are generated through an injected abstraction.
- Login fallback timestamps use `TimeProvider`.

#### Foundation for Next Step

Security handlers are deterministic and testable at their boundaries.

#### Affected Files

- `src/ShortenLink.Core/Abstractions/ISecureTokenGenerator.cs`
- `src/ShortenLink.AspNetCore/SecureTokenGenerator.cs`
- Security Application handlers.

#### Verification

Covered by build and API security tests.

#### Done Notes

Completed 2026-07-26.

### 020_007 - Normalize Admin/User Roles and Per-Link Access Levels

#### Step Goal

Make Admin/User the effective system-role model and keep View/Edit scoped to
individual link shares.

#### Scope

In: role catalog compatibility cleanup and access-level documentation.
Out: removing legacy serialized role identifiers in this migration.

#### Acceptance Criteria

- Permission bundles contain only Admin and User.
- Link sharing continues to use `ShortLinkShareAccess.View/Edit`.

#### Foundation for Next Step

Authorization checks have one consistent role/access vocabulary.

#### Affected Files

- `src/ShortenLink.Core/Security/ShortenLinkSystemRoles.cs`
- `src/ShortenLink.AspNetCore/ShortenLinkPermissions.cs`

#### Verification

Covered by API security and sharing tests.

#### Done Notes

Completed 2026-07-26; legacy aliases remain source-compatible but are not
permission bundles.

### 020_008 - Add Mediator Pipeline Behaviors and Close Verification

#### Step Goal

Centralize validation, diagnostics, and transaction cross-cutting concerns in
the custom Mediator.

#### Scope

In: validation marker seam, logging behavior, transaction behavior, and open
generic registration. Out: replacing the custom Mediator.

#### Acceptance Criteria

- Pipeline behaviors execute for mediated requests.
- Open generic registrations are valid in ASP.NET Core DI.

#### Foundation for Next Step

Future use cases inherit consistent cross-cutting behavior automatically.

#### Affected Files

- `src/ShortenLink.Application/Behaviors/`
- `src/ShortenLink.Api/MediatorServiceCollectionExtensions.cs`

#### Verification

```powershell
dotnet build ShortenLink.slnx --no-restore --verbosity minimal
dotnet test ShortenLink.slnx --no-build --no-restore --verbosity minimal
```

#### Done Notes

Completed 2026-07-26; build passed with 0 warnings/errors and all 160 tests
passed.

## Scan Rule

Read this file before working on a task. Complete one task at a time and keep
all architecture migration work inside Phase 020 until every done criterion is
verified.
