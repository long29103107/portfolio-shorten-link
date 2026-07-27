# Release Readiness Closure

Date: 2026-07-15

This snapshot closes the current Shorten Link product definition. It maps the
`PRODUCT_VISION.md` definition of done to concrete repo evidence and separates
completed package-readiness work from future live publishing decisions.

## Current Status

The current product definition is release-ready for handoff, local package
consumption, local feed rehearsal, or a future intentional NuGet.org publish
decision.

Live NuGet.org publishing is not part of the default repo workflow. It remains a
manual maintainer action that requires registry ownership, credentials supplied
outside source control, and explicit publish intent.

## Definition Of Done Evidence

| Product definition item | Evidence |
|---|---|
| Reusable library exposes stable models, services, repositories, options, DI setup, and endpoint mapping. | `src\ShortenLink.Core`, `src\ShortenLink.Infrastructure`, and `shared\ShortenLink.Hosting` provide the reusable package boundary. README documents `IShortLinkService`, `AddShortenLink(builder.Configuration)`, `UseShortenLinkRateLimiting()`, and `MapShortenLinkEndpoints()`. |
| Reusable library is isolated from the demo app and can be packed as NuGet packages. | `ShortenLink.Core`, `ShortenLink.Infrastructure`, and `ShortenLink.Hosting` are packable. `ShortenLink.Api` and `ShortenLink.Web` are documented as demo apps, not reusable package surface. |
| Demo API proves create, detail, delete/deactivate, and redirect flows. | `src\ShortenLink.Api` maps `POST /api/short-links`, `GET /api/short-links/{code}`, `DELETE /api/short-links/{code}`, and `GET /{code}`. API tests and consumer smoke cover the flow. |
| React demo proves create, copy, inspect, and fallback behavior. | `src\ShortenLink.Web` contains the create page, detail page, fallback page, and short-link components. README documents the Vite demo flow. |
| SQLite works by default. | README documents SQLite default configuration. Consumer smoke runs SQLite default mode with no PostgreSQL, Redis, Docker, frontend assets, credentials, or package publishing. |
| PostgreSQL can be enabled by configuration. | README documents `ShortenLink:Database:UsePostgres` and the PostgreSQL host smoke command. `scripts\smoke-postgres-host.ps1` verifies the provider path when a PostgreSQL instance is reachable. |
| Optional analytics and cache are designed as abstractions and implemented. | README documents opt-in click analytics, async worker behavior, memory cache, Redis cache, and cache invalidation behavior. Phase 003 records the production-readiness completion. |
| Tests cover important core logic and persistence behavior. | `tests\ShortenLink.Core.Tests`, `tests\ShortenLink.Infrastructure.Tests`, and `tests\ShortenLink.Api.Tests` are included in the solution. Recent task verification recorded 69 passing tests. |
| README explains how to run, configure, test, and reuse the library. | README covers build/test/pack, package selection, local package installation, ASP.NET Core setup, provider configuration, operational stack, consumer smoke, release dry-run, manual publish guardrails, and local feed rehearsal. |

## Package And Release Evidence

| Capability | Evidence |
|---|---|
| Consumer package entry point | `ShortenLink.Hosting` is the normal package for ASP.NET Core hosts. |
| Package metadata and README inclusion | `scripts\release-dry-run.ps1` inspects package IDs, versions, authors, README inclusion, license expression, repository metadata, tags, dependency shape, assemblies, and absence of demo API/Web coupling. |
| Clean consumer install | `scripts\smoke-consumer-package.ps1` creates a clean ASP.NET Core app, installs `ShortenLink.Hosting`, maps package endpoints, and verifies create/detail/redirect/deactivate/post-delete redirect behavior. |
| Manual publish guardrails | `scripts\publish-nuget.ps1` previews by default, requires `-Publish`, requires `NUGET_API_KEY` or `-NuGetApiKey`, masks the key in displayed command arguments, and reruns release dry-run before `dotnet nuget push`. |
| Live publish preflight | `docs\nuget-publish-preflight.md` separates repository-verifiable gates from external NuGet.org account, package ID ownership, API key scope, version availability, and maintainer approval checks. |
| Local feed rehearsal | `scripts\rehearse-local-feed.ps1` validates packages, copies the three reusable packages to `.tmp\local-nuget-feed`, handles duplicate versions with `-ResetFeed` or `-SkipDuplicate`, and smokes a clean consumer from the existing feed. |
| CI validation | `.github\workflows\ci.yml` runs restore, build, test, and pack on push and pull request without Docker, Redis, PostgreSQL, secrets, publishing, or deployment. |

## Maintainer Verification Commands

Run from the repository root before handoff or before any future publish
decision:

```powershell
dotnet build ShortenLink.slnx --verbosity minimal
dotnet test ShortenLink.slnx --verbosity minimal
dotnet pack ShortenLink.slnx -c Release --verbosity minimal
powershell.exe -ExecutionPolicy Bypass -File .\scripts\release-dry-run.ps1
powershell.exe -ExecutionPolicy Bypass -File .\scripts\rehearse-local-feed.ps1 -PackageVersion 1.0.0 -ResetFeed
powershell.exe -ExecutionPolicy Bypass -File .\scripts\smoke-consumer-package.ps1
```

The direct script form, such as `.\scripts\release-dry-run.ps1`, is also valid
when the local PowerShell execution policy allows it.

## Known Warnings And Environment Notes

- `dotnet pack ShortenLink.slnx -c Release --verbosity minimal` can emit a
  warning for `ShortenLink.Api` because the demo host has packaging disabled.
  This is expected. The reusable packages are `ShortenLink.Core`,
  `ShortenLink.Infrastructure`, and `ShortenLink.Hosting`.
- Restore-heavy commands may need network access to `api.nuget.org` for package
  dependencies and repository signature checks. In sandboxed environments, a
  `NU1301` network error should be treated as an environment blocker before it is
  treated as a repository regression.
- The Docker Compose smoke requires a reachable Docker daemon. It is optional
  and does not replace the default SQLite developer path.
- The PostgreSQL host smoke requires a reachable PostgreSQL instance and a valid
  connection string.
- The frontend build requires `src\ShortenLink.Web` dependencies to be restored
  with `npm install`.

## Future Optional Work

The current product definition does not require these items:

- Live NuGet.org publishing.
- Internal feed credentials or organization package registry setup.
- Automatic publish-on-push, publish-on-tag, GitHub Releases, or production
  deployment automation.
- SaaS billing, tenants, authentication, authorization, or advanced analytics
  dashboards.

If live publishing becomes desired later, open a separate phase only after
the `docs\nuget-publish-preflight.md` checklist confirms registry ownership,
package ID ownership, API key handling, version choice, and maintainer approval.
