# ADR 028-012: Security Package Boundary Decision

Date: 2026-08-06  
Status: Accepted - extraction deferred

## Context

The security surface spans domain policy, application use cases, persistence,
and ASP.NET request adapters. Phase 028 requires those responsibilities to be
audited without changing authentication algorithms, permission semantics,
routes, ownership rules, or sharing behavior. A new package is allowed only
when a concrete reusable consumer proves that the boundary is worth the extra
versioning and mapping surface.

## Responsibility and dependency map

| Layer | Security responsibility | Dependencies | Boundary decision |
| --- | --- | --- | --- |
| Core | Security users, API-key and sharing entities, roles, permissions, and policy catalogs | Core domain/value types only | Remains the provider-neutral, packable security model. |
| Application | Login/refresh and security-management commands, queries, current-user and session abstractions | Core, auditing, mediator contracts | Owns use-case orchestration; it does not know EF Core, ASP.NET, or token transport details. |
| Infrastructure | EF entities, schema mappings, and security repositories | Core, EF Core, configured persistence provider | Remains the persistence adapter; repository implementations do not flow into Core or Application. |
| Hosting | HTTP authorization/session services, token validation and creation, request-context adapter, options, and DI composition | ASP.NET Core, Application, Infrastructure, Core | Remains the host adapter because it requires `HttpContext`, hosting options, and concrete repository/session composition. |
| API | Route mapping and request/response binding for security endpoints | Application and Hosting | Remains a composition host; no security algorithm or policy moves into the API. |

The graph is acyclic: Core has no Application, Infrastructure, Hosting, or
ASP.NET reference; Application references Core and Auditing; Infrastructure
references Core and persistence libraries; Hosting composes Application,
Infrastructure, and ASP.NET; the API composes Hosting. There is one concrete
API host and no second independent .NET consumer of the Hosting security
services.

## Redirect-only audit

`AddShortenLink(..., options => options.RedirectOnly = true)` already omits the
security repositories, `IShortenLinkAuthorizationService`, and
`IShortenLinkUserSessionService`. The session adapter for the Application
mediator is now registered in the same full-persistence/full-security branch,
so redirect-only and external-persistence hosts do not carry an unusable
admin/session dependency. The request-context seam remains registered for
custom host consumers; removing it would be a public composition change even
though the built-in redirect endpoint does not resolve it.

## Decision

Do not extract a security package in `028_012`. The existing Core security
model is already the reusable seam. Hosting security services cannot be moved
to a host-neutral package without importing ASP.NET, Application abstractions,
or Infrastructure repositories, or introducing a second adapter layer. No
authentication, authorization, redirect, ownership, sharing, route, or JSON
behavior changes are part of this audit.

The redirect-only registration is tightened only to avoid registering the
Application session adapter when its concrete Hosting session service is
intentionally absent. Existing consumer overrides remain honored through
`TryAdd*` registrations.

## Reopen criteria

Revisit extraction when all of the following are true:

1. A second independent host or package consumes the same security seam.
2. The selected contract has no ASP.NET, EF Core, Hosting, or concrete
   repository dependency and can remain acyclic.
3. Authentication/session and authorization characterization tests prove that
   the extracted seam preserves token, permission, ownership, sharing, and
   route behavior, followed by an independent package build and consumer
   smoke test.

Until then, another package would be speculative and would duplicate the
existing Core boundary.
