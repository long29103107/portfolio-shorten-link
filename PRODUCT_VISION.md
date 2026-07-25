# PRODUCT_VISION.md - Shorten Link Library + Admin Demo App

## 1. Product Vision

Build a reusable .NET short-link library that can be packed as NuGet and embedded into other ASP.NET Core applications, plus a polished authenticated workspace/admin app that proves the real product workflow: create random short links, manage personally owned and shared URLs, redirect users, administer identities, and inspect operational state.

The product should not stay as a toy URL shortener. The long-term goal is a clean, configurable platform component with stable DI contracts, pluggable persistence/cache/analytics, production-aware defaults, and an admin experience that feels like a small control panel rather than a loose demo page.

## 2. Target Users

- .NET developers who want to add short-link behavior to an existing service.
- Backend/API teams that need a DI-configurable short-link module.
- Signed-in users who need a private short-link area and controlled View/Edit sharing with other users.
- Internal administrators who need unrestricted access to links, identities, permissions, and operational health.
- Maintainers who need a testable codebase with clear local SQLite defaults and an upgrade path to PostgreSQL/Redis.

## 3. Product Principles

- Library-first: core short-link behavior belongs in reusable library projects, not the demo app.
- Random-code-first: generated codes are always random; custom aliases are intentionally out of scope.
- Config-driven: database provider, redirect fallback, cache, analytics, and rate limits are controlled by configuration.
- Thin demo app: the API and React app prove behavior without duplicating core business logic.
- Use-case-first application layer: HTTP endpoints dispatch commands/queries
  through the dependency-free custom Mediator; each feature file keeps its
  request and handler together.
- Centralized failures: application handlers throw typed Core exceptions with
  catalogued error codes, and one API exception handler owns HTTP error mapping.
- Safe defaults: SQLite should work locally out of the box; PostgreSQL and Redis are optional production upgrades.
- Clear contracts: endpoint, model, service, repository, options, and DI APIs should remain stable for consumers.
- Uniform entity identity: every persisted DbSet entity is flattened directly under `ShortenLink.Core/Domain`, uses the `*Entity` suffix, derives from `BaseEntity<Guid>`, and uses a UUIDv7 surrogate primary key; domain-facing codes, user ids, role ids, credential hashes, and former composite keys remain unique business keys.
- Admin usability: the admin UI should be compact, operational, and fast to scan.
- Ownership-first: every short link belongs to its creator; access to another user's link must come from an explicit per-link share.
- Small global role model: `Admin` and `User` are the only system roles; `View` and `Edit` are per-link access levels, not global roles.
- Explicit privilege: Admin bypasses ownership/share checks and has full system access; User access remains scoped to owned or shared links.
- Role-gated administration: identity and role management is an intrinsic Admin capability, not a toggleable application permission.
- Compact permissions: link lifecycle uses one status permission, export follows read access, import has an explicit mutation permission, and audit/report visibility is available to both system roles.
- Production-aware: early phases can be simple, but the design must leave room for analytics, auth, cache, background processing, rate limiting, auditability, and CI.

## 4. Current Core Capabilities

- Create random short links from valid HTTP/HTTPS destination URLs.
- Require both Destination URL and Expiry for create/update.
- Generate Base62 random short codes with default length 7.
- Redirect from `/{code}` to the original destination.
- Handle unknown, inactive, and expired links with configured fallback behavior.
- Store links with SQLite by default, with PostgreSQL support by configuration.
- Build fresh SQLite/PostgreSQL schemas directly from the EF model; legacy runtime table-patching helpers are not part of the current clean-migration contract.
- Expose `AddShortenLink(...)` for ASP.NET Core consumers while the application host owns explicit endpoint mapping.
- Package the reusable library surface as NuGet packages.
- Provide a React admin UI for creating and managing generated short links.
- Support admin actions: create, edit, activate, deactivate, delete, bulk actions, copy, pagination, required-field validation, and toast feedback.
- Authenticate users with persisted identities and sessions.
- Seed a bootstrap `admin` identity assigned to the full-access `Admin` system role for a fresh local database.
- Attribute every newly created short link to the signed-in creator.
- Scope normal users to links they own or links explicitly shared with them.
- Share individual links with another user at `View` or `Edit` access.
- Allow Admin to bypass ownership and sharing restrictions across the system.
- Provide separate Short Links and Admin navigation, with Admin entry points visible only to authorized identities.
- Use email as the sign-in identifier; a fresh database seeds `admin@shortenlink.local` with the Admin role.
- Provide tests for generator, validation, service behavior, endpoint behavior, and persistence.
- Keep Minimal API endpoint groups thin: session, API-key, role, user,
  assignment, link-management, sharing, analytics, lifecycle, and redirect
  flows all dispatch through the Application layer.
- Redirect successful login to the personal `/` workspace.
- Provide reusable portal tooltip feedback for compact copy actions.

## 5. Product Gaps And Next Opportunities

These are the most valuable improvements from the current app state.

### P0 - Must Have Before Real Internal Use

- Complete authorization hardening for every link mutation and administrative endpoint.
- Audit ownership, share changes, authentication, and administrative mutations.
- Analytics in admin: expand click count and recent activity into an operational activity view.
- Robust server error UX: provide retry actions and avoid repeated duplicate toast spam during outages.
- Validation parity: keep FE and BE validation rules aligned and map backend field errors to the correct input.

### P1 - Strong Product Polish

- QR code generation for every short link.
- Copy short URL directly after create success.
- Better expiry controls: date presets, clear timezone display, and "expired soon" badges.
- Bulk export CSV for admin lists.
- Audit log for create, update, activate, deactivate, and delete actions.
- Dashboard metrics: total links, active links, expired links, total clicks, and recent activity.

### P2 - Scale And Operations

- Redis cache provider for redirect lookups.
- Async click-tracking worker so redirects do not wait on analytics persistence.
- Rate limit visibility in admin.
- Docker Compose for API, frontend, PostgreSQL, and Redis.
- CI coverage for build, tests, pack, and smoke flows.

## 6. Desired Architecture

```txt
shared/
  ShortenLink.Mediator/          # Dependency-free mediator contracts and dispatcher

src/
  ShortenLink.Core/              # Domain entities, contracts, exceptions, validation, core services
  ShortenLink.Application/       # Vertical feature commands/queries and application ports
  ShortenLink.Infrastructure/    # EF mapping, repositories, SQLite/PostgreSQL providers
  ShortenLink.AspNetCore/        # DI setup, options, authorization, middleware helpers
  ShortenLink.Worker/            # Optional analytics/background jobs
  ShortenLink.Api/               # Thin HTTP host, endpoint groups, adapters, global exception handling
  ShortenLink.Web/               # React + Vite frontend

tests/
  ShortenLink.Core.Tests/
  ShortenLink.Application.Tests/
  ShortenLink.Infrastructure.Tests/
  ShortenLink.Api.Tests/
```

## 7. Roadmap And Phase Goals

### Phase 1 - MVP Library And Demo Flow

Goal: deliver a reusable core short-link library with SQLite persistence and a working demo flow from React form to API creation to redirect.

Scope:

- Create the solution/project structure for core library, infrastructure, ASP.NET Core integration, demo API, demo Web, and tests.
- Keep library projects separate from demo API/Web so they can be packed and consumed independently.
- Add package metadata for the reusable library surface.
- Implement `ShortLink`, request/result DTOs, and core service contracts.
- Implement Base62 random short-code generation with default length 7.
- Reject empty, malformed, and non-HTTP/HTTPS URLs.
- Require expiry on create and update.
- Implement duplicate-code retry.
- Implement EF Core SQLite persistence with required indexes.
- Expose DI registration through `AddShortenLink(builder.Configuration)`.
- Implement demo API endpoints for create, list, detail, update, activate, deactivate, delete, and redirect.
- Implement redirect behavior for active non-expired links.
- Implement fallback behavior for unknown code through config.
- Build React pages for create, admin management, detail, and fallback/not-found.
- Add focused tests for generator, URL validation, create/resolve/update service, endpoint contracts, and SQLite integration.
- Add README instructions for local SQLite run, tests, package creation, and consumer usage.

Success criteria:

- Solution builds successfully.
- Demo API runs with SQLite default config.
- React app can create a short link through the API.
- `GET /{code}` redirects to the original URL.
- Create/update require Destination URL and Expiry.
- Unknown code follows configured fallback behavior.
- Library can be referenced by another .NET project through DI.
- Reusable library can be packed with `dotnet pack`.
- Minimum core, endpoint, and SQLite tests pass.

### Phase 2 - Admin Control Panel

Goal: turn the demo UI into a usable internal admin control panel for generated random short links.

Scope:

- Build compact shadcn-style admin layout.
- List generated short links with pagination.
- Create and update links from modal dialogs.
- Validate field-level errors under the matching input.
- Provide action dropdowns for edit, activate/deactivate, and delete.
- Provide copy icon action with copied feedback.
- Support bulk select and bulk operations.
- Show toasts for success, warning, info, and error states.
- Use tinted toast backgrounds with clear variant borders.
- Keep custom alias removed from the product and UI.

Success criteria:

- Admin can manage URLs without custom aliases.
- Validation errors stay inside the popup and attach to the correct input.
- Non-validation failures close the popup and show toast feedback.
- Create success reloads the list.
- Update success updates the row without unnecessary list disruption.
- Toast variants are visually distinct but not visually overpowering.

### Phase 3 - PostgreSQL Toggle

Goal: allow the same library and demo API to switch between SQLite and PostgreSQL by configuration only.

Scope:

- Add PostgreSQL EF Core provider support.
- Implement `ShortenLink:Database:UsePostgres` toggle.
- Keep repository/service/API contracts unchanged.
- Add configuration samples for both providers.
- Document migration/update commands for both providers.
- Add integration verification for provider selection where practical.

Success criteria:

- App runs with SQLite by default.
- App runs with PostgreSQL when enabled and configured.
- No code changes are required to switch database provider.
- Unique code and date indexes exist for both providers.

### Phase 4 - Analytics And Admin Insights

Goal: make the app useful beyond CRUD by showing how links are used.

Scope:

- Implement click tracking abstraction.
- Track click count, clicked timestamp, user agent, referer, and safe request metadata.
- Add async worker/channel path so redirect is not slowed by analytics persistence.
- Add admin columns for click count and last clicked.
- Add detail view for recent clicks.
- Add dashboard metrics for active links, expired links, total clicks, and recent activity.

Success criteria:

- Redirect remains fast even when analytics is enabled.
- Admin can identify popular, unused, expired, and recently clicked links.
- Analytics can be disabled by config for simple deployments.

### Phase 5 - Production Readiness

Goal: harden the system for real-world service usage.

Scope:

- Harden the existing admin authentication/authorization boundary.
- Establish the two-role system model: `Admin` for unrestricted administration and `User` for ownership-scoped workspace access.
- Persist creator ownership and per-link `View`/`Edit` shares.
- Enforce owner/share/Admin access consistently across list, detail, analytics, update, lifecycle, delete, and sharing endpoints.
- Implement cache abstraction for redirect lookup.
- Add in-memory cache and Redis provider.
- Ensure cache lookup happens before database lookup.
- Ensure cache invalidates on update, delete, activate, and deactivate.
- Add rate limiting for create and redirect-sensitive endpoints.
- Add audit log for admin mutations.
- Add Docker Compose for API, frontend, PostgreSQL, and Redis.
- Add GitHub Actions CI for build, test, pack, and smoke flows.

Success criteria:

- Admin routes are protected.
- Every created link is attributed to its creator.
- Users see and operate only on owned links or links shared at a sufficient per-link access level.
- Admin can operate across all links without an ownership/share grant.
- `View` and `Edit` remain link access levels and never appear as system roles.
- Cache miss falls back to database and stores successful lookup.
- Mutations invalidate cache correctly.
- Redis can be enabled by config.
- Local production-like stack starts from documented Docker Compose commands.
- CI validates important flows on every PR.

## 8. Non-Goals

- Public SaaS billing or tenant management.
- Multi-workspace or organization tenancy; each account has one private personal link area.
- Custom aliases.
- Public anonymous admin access.
- Advanced marketing landing pages.
- Distributed key coordination beyond retry-based uniqueness.
- Hard dependency on PostgreSQL, Redis, or worker infrastructure for local development.

## 9. Definition Of Done For The Product

The product is considered complete for this vision when:

- The reusable library exposes stable models, services, repositories, options, and DI setup; the API host owns endpoint presentation.
- API endpoint groups contain HTTP binding/response concerns only and dispatch
  business use cases through `ShortenLink.Mediator`.
- Commands and queries are implemented as vertical slices in
  `ShortenLink.Application/Features`, with request and handler kept together.
- Typed Core exceptions use centralized error-code constants and are converted
  by one global API exception handler.
- The reusable library is isolated from the demo app and can be packed as NuGet.
- The demo API proves create, list, detail, update, activate/deactivate, delete, and redirect flows.
- The React admin proves users can create, validate, copy, edit, activate/deactivate, delete, search, filter, and inspect short links.
- SQLite works by default.
- PostgreSQL can be enabled by configuration.
- Admin routes are protected.
- The only system roles are `Admin` and `User`.
- Security administration is Admin-only by role; the shared application catalog contains read, create, update, status, delete, import, analytics, and audit permissions.
- Export is covered by `short_links.read`; activate/deactivate share `short_links.status`.
- Link ownership is persisted, and per-link sharing supports `View` and `Edit`.
- Authorization follows the invariant: Admin has unrestricted access; User manages owned links and accesses other links only through an adequate share.
- Analytics and cache are implemented as configurable abstractions.
- Tests cover core logic, endpoint behavior, persistence, and the most important admin workflows.
- README explains how to run, configure, test, pack, publish, and reuse the library.
