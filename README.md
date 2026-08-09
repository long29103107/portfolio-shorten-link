# Shorten Link

Reusable .NET short-link library plus a demo ASP.NET Core API and React frontend.

The reusable library projects are intentionally separated from the demo application so they can be packed as NuGet packages and consumed by other .NET applications. `ShortenLink.Hosting` is the normal package for ASP.NET Core hosts; the demo API and React app exist to prove the package behavior, not to own short-link business logic.

See [CONTRIBUTING.md](CONTRIBUTING.md) for the Git commit convention used by
this repository.

## Project Structure

```text
shared/
  ShortenLink.Auditing/          # Reusable audit contracts, writer, and buffering
  ShortenLink.Mediator/          # Dependency-free request/handler mediator
  ShortenLink.Hosting/           # ASP.NET Core DI, authorization, and host integration

src/
  ShortenLink.Core/              # Domain entities, contracts, exceptions, and core services
  ShortenLink.Application/       # Feature commands/queries and application abstractions
  ShortenLink.Infrastructure/    # Persistence and provider adapters
  ShortenLink.Api/               # Thin ASP.NET Core host, endpoint groups, and exception mapping
  ShortenLink.Web/               # Demo React + Vite frontend

tests/
  ShortenLink.Core.Tests/
  ShortenLink.Application.Tests/
  ShortenLink.Infrastructure.Tests/
  ShortenLink.Api.Tests/
```

The demo backend follows a Clean Architecture dependency direction. Minimal API
endpoints bind HTTP input and dispatch a command/query through
`ShortenLink.Mediator`; handlers live beside their request in
`ShortenLink.Application/Features`. Handlers return application data and throw
typed Core exceptions. One global API exception handler owns HTTP error mapping,
so endpoint files do not contain repository logic or repeated error branches.

## Package Surface

| Package | Use when |
|---|---|
| `ShortenLink.Hosting` | You are building an ASP.NET Core host and want DI registration, options binding, authorization, redirect fallback, analytics worker integration, cache wiring, and rate limiting. |
| `ShortenLink.Auditing` | You need host-agnostic audit events, read contracts, buffering, writer, repository boundaries, or queue boundaries. See `shared/ShortenLink.Auditing/README.md`. |
| `ShortenLink.Core` | You need direct access to reusable domain models, validation, service contracts, request/result types, or `IShortLinkService` from non-host code. |
| `ShortenLink.Infrastructure` | You are composing persistence manually or extending provider wiring. Most ASP.NET Core hosts receive it transitively through `ShortenLink.Hosting`. |

`ShortenLink.Api` and `ShortenLink.Web` are demo applications and are not part of the reusable package surface.

## Build, Test, And Pack

Use these commands from the repository root.

### Build And Test

```powershell
dotnet build ShortenLink.slnx
dotnet test ShortenLink.slnx
```

### Pack The Consumer Package

The normal ASP.NET Core consumer entry point is `ShortenLink.Hosting`. It references the lower-level reusable projects and exposes host-facing extension methods.

```powershell
dotnet pack shared\ShortenLink.Hosting\ShortenLink.Hosting.csproj -c Release
```

The package is created at:

```text
shared\ShortenLink.Hosting\bin\Release\ShortenLink.Hosting.1.0.0.nupkg
```

Lower-level packages can also be packed when a consumer needs them directly:

```powershell
dotnet pack shared\ShortenLink.Auditing\ShortenLink.Auditing.csproj -c Release
dotnet pack src\ShortenLink.Core\ShortenLink.Core.csproj -c Release
dotnet pack src\ShortenLink.Infrastructure\ShortenLink.Infrastructure.csproj -c Release
```

Their default output paths are:

```text
shared\ShortenLink.Auditing\bin\Release\ShortenLink.Auditing.1.0.0.nupkg
src\ShortenLink.Core\bin\Release\ShortenLink.Core.1.0.0.nupkg
src\ShortenLink.Infrastructure\bin\Release\ShortenLink.Infrastructure.1.0.0.nupkg
```

To pack every packable project in the solution:

```powershell
dotnet pack ShortenLink.slnx -c Release
```

### Release-Readiness Verification

Before handing local packages to a consumer app, run:

```powershell
dotnet build ShortenLink.slnx --verbosity minimal
dotnet test ShortenLink.slnx --verbosity minimal
dotnet pack ShortenLink.slnx -c Release --verbosity minimal
.\scripts\release-dry-run.ps1
.\scripts\smoke-consumer-package.ps1
```

The build/test/pack commands validate the repository and package artifacts. The consumer smoke creates a clean app, installs the packaged `ShortenLink.Hosting` entry point from a local package source, and verifies create, detail, redirect, deactivate, and post-delete redirect behavior without using demo API internals.

The release dry-run script packs the reusable packages into `.tmp\release-dry-run`, inspects the package metadata and contents, confirms `README.md` is included, checks dependency shape, and verifies that demo API/Web artifacts are not coupled into the reusable packages. It never publishes to NuGet; passing `-Publish` fails closed.

Keep the dry-run package artifacts for inspection when needed:

```powershell
.\scripts\release-dry-run.ps1 -KeepArtifacts
```

### Release Checklist

Use this checklist before any future real package publish:

- Review package versions and release notes for `ShortenLink.Auditing`, `ShortenLink.Core`, `ShortenLink.Infrastructure`, and `ShortenLink.Hosting`.
- Complete the maintainer preflight in `docs\nuget-publish-preflight.md`, including package ID ownership, account or organization ownership, API key scope, version availability, and release approval.
- Run `dotnet build ShortenLink.slnx --verbosity minimal`.
- Run `dotnet test ShortenLink.slnx --verbosity minimal`.
- Run `dotnet pack ShortenLink.slnx -c Release --verbosity minimal`.
- Run `.\scripts\release-dry-run.ps1` and confirm it reports `Published: false`.
- Run `.\scripts\rehearse-local-feed.ps1 -PackageVersion <version> -ResetFeed` to prove the publish path against a local folder feed.
- Run `.\scripts\smoke-consumer-package.ps1` to validate a clean ASP.NET Core consumer installation.
- If publishing later, use a separate explicit publish command or workflow with a real NuGet API key, manual approval, and the package artifacts inspected by the dry-run.

NuGet publishing is intentionally out of scope for the default verification path. No script in this repository should publish packages unless a later task adds a credential-protected publish workflow deliberately.

### Manual NuGet Publish Workflow

Publishing is a maintainer-only operation. The default release commands stay dry-run-only, and `scripts\publish-nuget.ps1` only calls `dotnet nuget push` when a maintainer supplies both explicit intent and credentials.

Before any publish attempt:

- Complete `docs\nuget-publish-preflight.md` and stop if package ID ownership, credentials, version choice, or maintainer approval is missing.
- Review the package version and confirm it has not already been pushed to NuGet.
- Review release notes and public package metadata.
- Run `dotnet build ShortenLink.slnx --verbosity minimal`.
- Run `dotnet test ShortenLink.slnx --verbosity minimal`.
- Run `dotnet pack ShortenLink.slnx -c Release --verbosity minimal`.
- Run `.\scripts\release-dry-run.ps1 -PackageVersion <version>` and confirm it reports `Published: false`.
- Run `.\scripts\smoke-consumer-package.ps1 -PackageVersion <version>`.

Preview the publish command without pushing packages:

```powershell
.\scripts\publish-nuget.ps1 -PackageVersion 1.0.0
```

To publish intentionally, provide the API key from the environment or another secret store and pass `-Publish`:

```powershell
$env:NUGET_API_KEY = "<set outside source control>"
.\scripts\publish-nuget.ps1 -PackageVersion 1.0.0 -Publish
```

Use `-SkipDuplicate` only when retrying a partially completed publish and after confirming the already-published package version is expected:

```powershell
.\scripts\publish-nuget.ps1 -PackageVersion 1.0.0 -Publish -SkipDuplicate
```

The publish script fails closed when `-Publish` is missing or no NuGet API key is available. When publishing is enabled, it reruns the release dry-run into `.tmp\nuget-publish` before pushing `ShortenLink.Auditing`, `ShortenLink.Core`, `ShortenLink.Infrastructure`, and `ShortenLink.Hosting`.

After publishing, verify the packages on NuGet, install `ShortenLink.Hosting` into a clean consumer app, and run the create, detail, redirect, and deactivate smoke flow again. If a bad package is published, prefer deprecating or unlisting the affected version and publishing a corrected version; do not overwrite the same NuGet version.

### Local Feed Publish Rehearsal

Before using real NuGet credentials, rehearse the publish path against a local folder feed:

```powershell
.\scripts\rehearse-local-feed.ps1 -PackageVersion 1.0.0 -ResetFeed
```

The rehearsal validates packages with `release-dry-run.ps1`, copies `ShortenLink.Auditing`, `ShortenLink.Core`, `ShortenLink.Infrastructure`, and `ShortenLink.Hosting` into `.tmp\local-nuget-feed`, then runs the clean consumer smoke against that existing feed. It does not call `dotnet nuget push`, does not require credentials, and never publishes to NuGet.org.

If the feed already contains the same package version, the script fails closed. Start a clean rehearsal feed with `-ResetFeed`, or intentionally retry against existing packages with `-SkipDuplicate`:

```powershell
.\scripts\rehearse-local-feed.ps1 -PackageVersion 1.0.0 -SkipDuplicate
```

Keep rehearsal artifacts for inspection when needed:

```powershell
.\scripts\rehearse-local-feed.ps1 -PackageVersion 1.0.0 -ResetFeed -KeepArtifacts
```

## Use From Another .NET App

Most ASP.NET Core consumers should start with `ShortenLink.Hosting`. That package is the host-facing entry point for dependency injection and runtime integration. Endpoint presentation is composed explicitly by the application host.

### Consumer Package Smoke

To validate the package from a clean consumer app shape, run:

```powershell
.\scripts\smoke-consumer-package.ps1
```

The smoke script packs the reusable packages into a temporary local NuGet source, creates a clean ASP.NET Core app under `.tmp`, installs `ShortenLink.Hosting`, maps the library endpoints, runs SQLite default mode, and verifies create, detail, redirect, and deactivate behavior. It does not reference `ShortenLink.Api`, and it does not require PostgreSQL, Redis, Docker, frontend assets, credentials, or package publishing.

To smoke an existing local package source without regenerating it:

```powershell
.\scripts\smoke-consumer-package.ps1 -PackageSource .\.tmp\local-nuget-feed -UseExistingPackageSource
```

Keep the generated consumer app and local package source for inspection when needed:

```powershell
.\scripts\smoke-consumer-package.ps1 -KeepArtifacts
```

### Option 1: Project Reference During Local Development

From the consumer app directory:

```powershell
dotnet add reference ..\shorten-link\shared\ShortenLink.Hosting\ShortenLink.Hosting.csproj
```

### Option 2: Install From A Local NuGet Folder

Create a local package folder that contains all reusable packages:

```powershell
New-Item -ItemType Directory -Force .\.nupkg
dotnet pack ..\shorten-link\ShortenLink.slnx -c Release -o .\.nupkg
```

Add that folder as a NuGet source for the consumer app:

```powershell
dotnet nuget add source .\.nupkg --name shorten-link-local
```

Install the package:

```powershell
dotnet add package ShortenLink.Hosting --source .\.nupkg
```

If the consumer needs generic audit contracts, install/reference `ShortenLink.Auditing`. For short-link domain contracts, install/reference `ShortenLink.Core`; for manual EF Core composition, install/reference `ShortenLink.Infrastructure`. Normal API hosts should start with `ShortenLink.Hosting`.

### ASP.NET Core Setup

In the consumer app's `Program.cs`:

```csharp
using ShortenLink.Hosting;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddShortenLink(builder.Configuration);

var app = builder.Build();

app.UseRateLimiter();
app.MapShortenLinkEndpoints();

app.Run();
```

`AddShortenLink` registers the default HTTP request context and the built-in
session/API-key authorization evaluator through `TryAdd`. An external host can
bring its own identity and policy system by registering replacements before
the package:

```csharp
builder.Services.AddScoped<ICurrentRequestContext, ClaimsPrincipalRequestContext>();
builder.Services.AddScoped<IShortenLinkAuthorizationService, PolicyAuthorizationService>();
builder.Services.AddShortenLink(builder.Configuration);
```

The replacement authorization service returns `ShortenLinkAuthorizationResult`
with `Success(...)`, `Unauthorized()`, or `Forbidden()`. Application handlers
continue to use the same permission and ownership contracts, so the consumer
does not need to reference `ShortenLink.Api` or copy its request-context adapter.

For a host that only resolves redirects, omit the security persistence and
bootstrap-admin path while keeping the same redirect handler:

```csharp
builder.Services.AddShortenLink(
    builder.Configuration,
    options => options.RedirectOnly = true);

app.MapShortenLinkEndpoints(options =>
{
    options.MapManagementEndpoints = false;
});
```

The default full profile remains unchanged when the overload is not used.

Providers that own persistence can opt out of EF registration and supply the
public Core contracts (`IShortLinkRepository`, `IUnitOfWork`, click/share/audit
repositories) through DI:

```csharp
builder.Services.AddShortenLink(
    builder.Configuration,
    options => options.UseExternalPersistence = true);
```

External providers own transactions, concurrency, schema/migrations, and the
durability guarantees of their repository implementations.

The maintained EF adapters are covered by the infrastructure test suite. Run
the SQLite contract-backed checks locally with:

```powershell
dotnet test tests\ShortenLink.Infrastructure.Tests\ShortenLink.Infrastructure.Tests.csproj --no-restore --verbosity minimal
```

PostgreSQL adapter verification uses the same repository contracts when a
PostgreSQL connection is supplied by the test environment; it is opt-in and
does not affect the SQLite default path.

Minimum `appsettings.json` configuration for SQLite default mode:

```json
{
  "ShortenLink": {
    "BaseUrl": "https://localhost:5001",
    "Code": {
      "DefaultLength": 7,
      "MaxRetry": 5
    },
    "Database": {
      "UsePostgres": false,
      "SqliteConnectionString": "Data Source=shorten-link.db",
      "PostgresConnectionString": "Host=localhost;Port=5432;Database=shorten_link;Username=postgres;Password=postgres"
    },
    "Redirect": {
      "EnableFrontendFallback": true,
      "FrontendFallbackPath": "/not-found"
    },
    "Analytics": {
      "Enabled": true,
      "UseAsyncWorker": true,
      "QueueCapacity": 512
    },
    "Cache": {
      "Enabled": false,
      "Provider": "Memory",
      "RedisConnectionString": "localhost:6379",
      "EntryTtlSeconds": 3600
    },
    "RateLimiting": {
      "Enabled": false,
      "Create": {
        "PermitLimit": 60,
        "WindowSeconds": 60,
        "QueueLimit": 0
      },
      "Redirect": {
        "PermitLimit": 120,
        "WindowSeconds": 60,
        "QueueLimit": 0
      }
    },
    "Security": {
      "Enabled": false,
      "HeaderName": "X-ShortenLink-Api-Key",
      "ApiKeys": [
        {
          "Name": "local-owner",
          "Key": "dev-owner-key",
          "Roles": [ "Admin" ],
          "Permissions": []
        }
      ]
    }
  }
}
```

`Code:DefaultLength` controls the length of generated random Base62 codes.
`Code:MaxRetry` limits how many candidates are checked when a generated code
already exists. The defaults are 7 and 10 respectively; the demo app overrides
`MaxRetry` to 5 in its local settings.

Lifecycle and redirect events are opt-in. Register an `IShortLinkEventSink`
before resolving the short-link service; the sink should enqueue work and
return promptly so event delivery does not add redirect latency. Event payloads
contain only a version, event type, short code, timestamp, expiry, and active
state; destination URLs, identities, credentials, tokens, hashes, and request
metadata are intentionally excluded.

### Validation Error Contract

Validation failures preserve the existing `errorCode` and `message` fields and may add a `fieldErrors` object when the server can identify specific request inputs:

```json
{
  "errorCode": "invalid_url",
  "message": "Original URL must be an absolute HTTP or HTTPS URL.",
  "fieldErrors": {
    "originalUrl": [
      "Original URL must be an absolute HTTP or HTTPS URL."
    ]
  }
}
```

Field keys use the JSON request-property casing, such as `originalUrl`, `expiredAtUtc`, `username`, `password`, `displayName`, `roleIds`, and `permissions`. More than one field can be reported when multiple inputs are independently known to be missing or invalid. Authentication, authorization, not-found, conflict, and operational failures omit `fieldErrors` unless a specific submitted field is deterministically responsible. Server validation remains authoritative; clients should use unknown errors as safe form-level fallbacks and must never infer or display submitted secret values from an error response.

### Password-Protected Redirects

Opening a password-protected short URL in a browser now returns a small unlock
form instead of exposing the JSON `password_required` response. The form posts
the password to the same short URL; a correct password redirects to the
destination, while an incorrect password keeps the form open. API and SDK
clients can continue using `X-Short-Link-Password` (or the existing `password`
query parameter) for programmatic redirects.

### Folders And Tags

Short links can carry one flat folder label and zero or more reusable tags.
Folder and tag values are trimmed, normalized case-insensitively, and returned
by create, update, detail, list, and export contracts. Tags are supplied as a
JSON array and duplicate values collapse to one normalized tag. Blank metadata
is valid for existing and newly created links.

```json
{
  "originalUrl": "https://example.com/campaign",
  "expiredAtUtc": "2026-12-31T00:00:00Z",
  "folder": "marketing",
  "tags": ["launch", "email"]
}
```

Management list calls can filter the authorized result set with
`GET /api/short-links?folder=marketing` or
`GET /api/short-links?tag=launch`; existing search, status, sort, pagination,
and ownership rules remain unchanged. Folder/tag taxonomy CRUD and nested
folders are intentionally outside this first slice.

### Bulk Operations

Authorized management clients can apply one bounded operation to 1-100 unique
short-link codes through `POST /api/short-links/bulk`:

```json
{
  "codes": ["abc1234", "def5678"],
  "operation": "organize",
  "folder": "marketing",
  "tags": ["launch", "email"]
}
```

Supported operations are `activate`, `deactivate`, `delete`, and `organize`.
`organize` replaces the selected folder and tags; send blank/null metadata to
clear them. Each item is authorized independently and the response reports
`requestedCount`, `succeededCount`, `failedCount`, and item-level errors, so a
missing or unauthorized code does not hide successful sibling results. The
management table also exports only the currently selected rows through its
`Export selected` action.

### Admin Security

The only system roles are:

- `Admin`: unrestricted system access, including identity/role administration,
  and bypass of link ownership/share checks.
- `User`: a private personal link area plus access to other users' links only
  when an explicit per-link `View` or `Edit` share exists.

`View` and `Edit` are per-link access levels, not global roles. Link permissions
cover read, create, update, status, delete, import, analytics, and audit/report
access. Export is covered by read permission, while activate and deactivate use
the same status permission. Security administration itself is Admin-only by
role.

Links use `AllowList` sharing by default. Owners and admins can switch a link
between `AllowList` and `Public` with `PUT /api/short-links/{code}/sharing-mode`
and `{ "mode": "Public" }` (or `AllowList`). Public mode grants every
authenticated workspace user `View` access; `Edit` access remains an explicit
per-user share. `PUT /api/short-links/{code}/shares` accepts one workspace
username/email per request, and the admin UI accepts multiple email addresses
separated by commas, spaces, or new lines.

The demo API enables security by default. Send either a signed user session
token as `Authorization: Bearer <token>` or an API key without a session. The
portable API-key forms are `Authorization: ApiKey <key>` and `X-Api-Key: <key>`;
the configured `ShortenLink:Security:HeaderName` header, which defaults to
`X-ShortenLink-Api-Key`, remains supported for compatibility. Reusable
consumers may choose their own configuration, but should keep security enabled
for any exposed management endpoint.

On a fresh database, startup seeds `admin@shortenlink.local` with password
`admin`, assigns the `Admin` role, and enables the full permission bundle. The
raw password is not stored; the database stores password hash material only.
Use this bootstrap account only for local development.

User-session APIs:

- `POST /api/security/login`
- `POST /api/security/refresh`
- `GET /api/security/me`

`POST /api/security/login` accepts `email` and `password`. Successful responses
return access/refresh tokens plus safe user metadata. The React app stores the
session and redirects to `/`, the personal create workspace. Login failures use
the same `invalid_login` response for unknown users, disabled users, and bad
passwords. Responses never include raw passwords, password hashes, or signing
material.

`GET /api/security/me` requires `Authorization: Bearer <token>` and returns the same safe current-user metadata. Protected admin endpoints can derive permissions from the logged-in user's system or custom roles. Session tokens are local/demo bearer credentials signed by the app; configure `ShortenLink:Security:SessionSigningKey` and `ShortenLink:Security:SessionTokenTtlMinutes` when security is enabled.

Configured API keys remain the bootstrap/local fallback path. The reusable persistence layer also supports durable security assignments for API-key credentials: assignments store a credential key hash, enabled state, built-in system roles, and optional explicit permissions. When a persisted assignment exists for a credential, it is the backend source for that credential; a disabled persisted assignment rejects the credential even if a matching bootstrap key is still present in configuration.

Admin can manage durable assignments through backend API contracts:

- `GET /api/security/assignments`
- `PUT /api/security/assignments`
- `POST /api/security/assignments/{credentialKeyHash}/disable`

The upsert request accepts a raw `credentialKey` only so the server can hash it before persistence. List and disable responses never return raw API keys; they expose the credential key hash, assignment name, built-in roles, explicit permissions, enabled state, and creation timestamp. Unknown roles or permissions are rejected with stable client errors.

Admin can also manage role and user identities through backend API contracts:

- `GET /api/security/roles`
- `PUT /api/security/roles/custom`
- `DELETE /api/security/roles/custom/{id}`
- `GET /api/security/users`
- `PUT /api/security/users`
- `POST /api/security/users/{id}/disable`

System roles are predefined bundles in the library and are returned as enabled,
non-deletable role bundles. Their permission overrides can be staged in the UI
and saved in one operation. Managed users receive `Admin` or `User`; user
responses never return raw passwords or password hashes. The bootstrap admin is
protected from normal update/disable operations.

Permissions are the source of truth for link operations, while security
administration is intrinsically Admin-only. OAuth/OIDC, public signup, password
reset, MFA, multi-workspace, and organization tenancy remain out of scope.

Logged-in users can manage their own API keys through backend API contracts:

- `GET /api/security/api-keys`
- `POST /api/security/api-keys`
- `PUT /api/security/api-keys/{id}`
- `POST /api/security/api-keys/{id}/disable`

These endpoints require `Authorization: Bearer <token>` from `POST /api/security/login`. `POST /api/security/api-keys` returns the raw API key only once in the create response; subsequent list, rename, and disable responses return metadata only and never include raw key material or key hashes. Store the raw key securely when it is created. Use the returned key on protected API calls with `Authorization: ApiKey <key>` or `X-Api-Key: <key>`; the configured `X-ShortenLink-Api-Key` header remains available for existing clients. Authorization resolves permissions from the owning user's enabled system and custom roles. Disabled keys and disabled users are rejected.

For example, an external application can call the management API without
creating a session:

```bash
curl https://localhost:5001/api/short-links?limit=25 \
  -H "Authorization: ApiKey slk_<stored-key>"
```

When using persisted user-owned API keys, `ShortenLink:Security:ApiKeys` may be
empty; configured keys are only the optional bootstrap/local fallback path.

When security is enabled, missing credentials return `401 unauthorized`; valid credentials without the required permission return `403 forbidden`. The React app routes those outcomes to `/unauthorized` and `/forbidden`.

The admin list endpoint uses `fe` as the single filter expression and `sort` as
the sort expression:

```bash
curl --get http://localhost:5000/api/short-links \
  --data-urlencode 'page=1' \
  --data-urlencode 'limit=25' \
  --data-urlencode 'fe=(((Code contains `docs`) | (OriginalUrl contains `docs`)) & (IsActive eq `true`))' \
  --data-urlencode 'sort=-CreatedAt'
```

Filter conditions use the following grammar shape, combined with `&`, `|`, `!`,
and grouped parentheses:

```text
(Field operator `value`)
```

Supported operators are `eq`, `ne`, `gt`, `ge`, `lt`, `le`, `contains`,
`startsWith`, and `in`. Values must be wrapped in backticks.

Short-link filters allow `Code`, `OriginalUrl`, `ExpiresAt`, `IsActive`,
`CreatedAt`, and `CreatedByUserId`. Use `+Field` or `-Field` for ascending or
descending sort. Sort supports `CreatedAt`, `ExpiresAt`, `OriginalUrl`, `Code`,
and `IsActive`. Clients derive status choices such as expired or expiring-soon
into `IsActive` and `ExpiresAt` conditions in `fe`.

Numbered list responses include `items`, `totalCount`, `page`, `pageSize`, and
`totalPages`, with counts calculated after applying `fe`. Invalid expressions or
non-whitelisted fields return `400` with a stable error code.

The React app includes `/login`; successful sign-in redirects to `/`. The `/`
workspace creates personal links, `/short-links` manages owned/shared links, and
`/admin/dashboard`, `/admin/security/users`, and `/admin/security/roles` are
Admin surfaces. The account dropdown switches between home, short-link
management, and Admin where authorized. Copy actions use compact icon buttons
with reusable portal feedback.

For local frontend development without backend security enabled, configure the React app with Vite environment variables instead of hard-coding secrets:

```dotenv
VITE_SHORTENLINK_ADMIN_API_KEY=dev-owner-key
VITE_SHORTENLINK_ADMIN_API_KEY_HEADER=X-ShortenLink-Api-Key
VITE_SHORTENLINK_ADMIN_ROLE=Admin
```

`VITE_SHORTENLINK_ADMIN_ROLE` accepts `Admin` or `User`. The UI uses the
signed-in user's roles and permissions first, then these local/demo fallback
values to hide or disable controls. For permission-level testing, use
`VITE_SHORTENLINK_ADMIN_PERMISSIONS` with comma-separated names such as
`short_links.read,short_links.create`.

If no frontend role or permission variables are set, the UI keeps all admin controls available for the default disabled-security demo mode. The API still enforces authorization whenever backend security is enabled.

### Direct Service Usage

Application services can depend on the reusable `IShortLinkService` contract directly. The intended shape is:

```csharp
using ShortenLink.Core.Services;

public sealed class MyLinkService
{
    private readonly IShortLinkService _shortLinkService;

    public MyLinkService(IShortLinkService shortLinkService)
    {
        _shortLinkService = shortLinkService;
    }

    public Task<CreateShortLinkResponse> CreateAsync(string url, CancellationToken cancellationToken = default)
    {
        return _shortLinkService.CreateAsync(
            new CreateShortLinkRequest(url),
            cancellationToken);
    }
}
```

Switch to PostgreSQL by configuration only:

```json
{
  "ShortenLink": {
    "Database": {
      "UsePostgres": true,
      "PostgresConnectionString": "Host=localhost;Port=5432;Database=shorten_link;Username=postgres;Password=postgres"
    }
  }
}
```

The demo host still uses `AddShortenLink(builder.Configuration);` with no application-code changes. On startup it calls `EnsureCreated()` for the selected provider, so SQLite remains the default local path while PostgreSQL can be enabled with a valid connection string.

The compatibility boundary in `ShortenLink.Infrastructure.Persistence.ShortLinkDatabaseSchema`
keeps the existing `EnsureCreated` startup path safe for databases created by
older releases. Provider-specific compatibility SQL is selected explicitly for
SQLite or PostgreSQL, and each upgrade is idempotent. The boundary only restores
the existing audit, expiration-checkpoint, tenant/idempotency, and UTC timestamp
schema; it does not create new product tables or change migration policy.

`IShortLinkService`, `CreateShortLinkRequest`, and `CreateShortLinkResponse` live in `ShortenLink.Core.Services`. Consumer code should continue to call the reusable service contract instead of re-creating short-link rules in the host app.

## Configuration Defaults And Optional Providers

SQLite is the safe default and requires no external infrastructure. PostgreSQL, Redis cache, click analytics, and rate limiting are opt-in through configuration. A consumer can install the same `ShortenLink.Hosting` package and choose behavior through `ShortenLink:*` settings instead of changing application code.

## Idempotent Create Requests

Create requests may include an `Idempotency-Key` header up to 256 characters:

```http
POST /api/short-links
Idempotency-Key: import-item-42
Content-Type: application/json
```

The first request persists the key with the generated link. An equivalent retry
returns the same link with HTTP `200` and does not create another link or audit
event. Reusing a key with a different destination, expiry, or actor returns the
stable `idempotency_conflict` error. Requests without the header retain the
normal random-code behavior and response status.

The reusable store boundary is `IShortLinkIdempotencyRepository`. Custom
providers that accept idempotency keys must implement its lookup and enforce a
unique key at their own persistence boundary, translating a concurrent winner
into `ShortLinkIdempotencyConflictException`. Keys are not returned in API
responses, lifecycle events, analytics, or diagnostic logs.

## Bulk Import Dry-Run

Administrators with `short_links.import` can validate a bounded import batch
without writing links, cache entries, audit events, or lifecycle events:

```http
POST /api/short-links/import/dry-run
Content-Type: application/json

{
  "items": [
    {
      "originalUrl": "https://example.com/docs",
      "expiredAtUtc": "2026-07-20T00:00:00Z",
      "idempotencyKey": "batch-item-1"
    }
  ]
}
```

The response contains `totalCount`, `validCount`, `invalidCount`, `truncated`,
and one `{ itemNumber, succeeded, errorCode, errorMessage }` result per
processed item. Validation is async-enumerable compatible and currently
bounded at 1,000 items. Errors are stable (`invalid_url`,
`invalid_expiration`, `invalid_idempotency_key`, or
`duplicate_idempotency_key`) and never echo URLs, keys, credentials, or other
input data. Persistence and streaming workers can consume this boundary in a
later import task.

To execute the bounded batch, use the same payload with the import endpoint:

```http
POST /api/short-links/import
Content-Type: application/json

{
  "items": [
    {
      "originalUrl": "https://example.com/docs",
      "expiredAtUtc": "2026-12-31T00:00:00Z",
      "idempotencyKey": "docs-1"
    }
  ]
}
```

Execution persists each valid item through the normal create/idempotency
boundary and returns `succeededCount`, `failedCount`, `replayedCount`, and
per-item `shortCode`/`replayed` fields. A validation, conflict, or persistence
failure is isolated to its item so later items continue. Replayed items reuse
their existing code and do not create another audit event; the endpoint never
returns original URLs or idempotency keys in error details.

## Streaming Bulk Export

Callers with `short_links.read` can stream their recent accessible links as a
JSON array:

```http
GET /api/short-links/export?limit=500
```

The application exposes records through an `IAsyncEnumerable` boundary and
pages through the provider-neutral recent-link service contract. Results use a
stable newest-first order. The default limit is 100 and requests are clamped to
1,000 records.

Each record contains safe link metadata including `code`, `originalUrl`,
`createdAtUtc`, `expiredAtUtc`, `isActive`, `accessLevel`, `folder`, and `tags`.
Creator identities, idempotency keys, shares, audit details, and other secrets
are never exported. Regular users receive only
owned or explicitly shared links; administrators retain their existing global
read scope.

## Optional Tenant Partitions

Tenancy is opt-in and trusted-host driven. The built-in request context returns
no tenant, so existing consumers keep the single-tenant behavior. A host that
already resolves tenants can include the normalized identifier when its custom
`ICurrentRequestContext` authorizes a request:

```csharp
return new CurrentRequestActor(
    userId,
    isAdmin,
    actorId,
    TenantId: resolvedTenantId);
```

Do not populate this value directly from an untrusted header without validating
that the authenticated caller belongs to that tenant. Tenant identifiers are
trimmed and limited to 128 characters.

The tenant flows through create and import persistence plus list/export access
scopes. Tenant mismatch is rejected before owner, share, or Admin bypass checks.
Idempotency keys are unique within a tenant, so two tenants may safely use the
same key; short codes remain globally unique so public redirects stay
unambiguous. Tenant identifiers are internal partition data and are not added to
link API responses or export records.

For hosts that need tenant-aware public redirects, implement the optional
`ICurrentRequestContext.GetCurrentTenantIdAsync` seam with a trusted, validated
tenant value. Resolve lookups use a tenant-specific cache key and verify the
tenant again after a database read, so a cache hit from another partition is
treated as not found. Providers that do not implement `ITenantAwareShortLinkCache`
are bypassed for tenant-aware requests. Click records carry the same partition
identifier and analytics queries use `ITenantAwareShortLinkClickRepository`;
legacy unscoped cache and analytics contracts remain source compatible for the
default single-tenant path.

Custom persistence providers must explicitly implement
`IShortLinkTenantRepository` before tenant-scoped requests are accepted. This
capability promises that tenant access scopes are enforced and supplies the
tenant-aware idempotency lookup. Providers that only implement the original
repository contracts remain valid for the default single-tenant path and fail
closed if a tenant-aware host tries to use them.

## Expiration And Retention Hooks

The optional `IShortLinkExpirationService` exposes a read-only, bounded batch
evaluation seam for future cleanup workers. A caller supplies the evaluation
instant, optional tenant, cursor, limit, and `ShortLinkRetentionPolicy`; the
result is deterministic and reports `skipped`, `retained`, or `expired` per
link. Limits are clamped to 500 and cursors are stable across expiration time
and code ordering.

Evaluation does not delete, deactivate, mutate analytics, or touch the cache.
Hosts may register `IShortLinkExpirationEventSink` to receive versioned,
secret-safe expiration metadata. Only records evaluated as `expired` request
cache invalidation; tenant identifiers remain partition metadata and are never
included in public link responses. Scheduler registration, destructive cleanup,
and retention policy administration remain outside this hook boundary.

The built-in host also exposes an explicit `POST
/api/short-links/expiration/execute` trigger for one bounded execution batch.
It requires `short_links.status`, resumes from a durable tenant-scoped
checkpoint by default, and advances that checkpoint only after the cache
invalidation handoff succeeds. The trigger never deletes or deactivates links;
automatic scheduling remains a future host concern. A request may provide
`evaluatedAtUtc`, `limit`, `retainExpiredForSeconds`, and
`resumeFromCheckpoint` for deterministic/manual execution.

## Observability And Health Checks

Observability is opt-in. The default configuration does not create redirect
activities or record meters, so a minimal host keeps the existing redirect
path. Enable the Core diagnostics when an OpenTelemetry listener or another
`ActivityListener`/`MeterListener` is registered:

```json
{
  "ShortenLink": {
    "Observability": {
      "Enabled": true,
      "HealthChecksEnabled": true
    }
  }
}
```

The stable diagnostics are `ShortenLink` (`ActivitySource` and `Meter`) with
the `ShortenLink.Redirect` activity and these low-cardinality meters:
`shortenlink.redirects`, `shortenlink.redirect.failures`,
`shortenlink.redirect.cache.hits`, and `shortenlink.redirect.cache.misses`.
Only cache-hit and outcome dimensions are emitted; destination URLs, short-link
codes, identities, request metadata, connection strings, credentials, tokens,
and exception messages are intentionally excluded.

When `HealthChecksEnabled` is true, `AddShortenLink` registers configuration,
database, cache, and analytics checks. A host can map them to its own route:

```csharp
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});
```

Hosts that need explicit registration can call
`builder.Services.AddShortenLinkHealthChecks()`; the extension is idempotent.
Health checks never return connection strings or provider exception messages.
Mediator diagnostics use stable event names (`ShortenLinkRequestCompleted` and
`ShortenLinkRequestFailed`) and log only request type, elapsed time, and safe
exception type.

## Click Analytics

Phase 3 adds an opt-in click analytics path for redirects:

- Leave `ShortenLink:Analytics:Enabled` as `false` to keep redirect behavior unchanged with no click persistence.
- Set `ShortenLink:Analytics:Enabled` to `true` to capture short code, click timestamp, remote IP, user agent, referrer, normalized device/browser/OS metadata, optional country code, and a hash-only visitor key for successful redirects.
- Leave `ShortenLink:Analytics:UseAsyncWorker` as `true` to enqueue analytics writes through the hosted background worker so redirect responses do not wait for database persistence.
- `ShortenLink:Analytics:QueueCapacity` controls the bounded in-memory queue used by the async worker.

Admins with `analytics.read` can inspect persisted click activity through:

- `GET /api/short-links/{code}/analytics`

The response includes `clickCount`, nullable `uniqueClickCount`,
`lastClickedAtUtc`, bounded `devices`, `browsers`, `operatingSystems`,
`referrers`, and `countries` breakdowns, plus a `recentClicks` list with the
enriched activity metadata. The visitor key hash is used only for distinct
counts and is never returned. Unique clicks mean distinct normalized IP + user
agent fingerprints; clicks without either value cannot be identified as
unique. Links with no clicks return zero counts, a null last-click timestamp,
empty breakdowns, and an empty recent-click list. When admin security is
enabled, missing credentials return `401 unauthorized`, and credentials
without `analytics.read` return `403 forbidden`.

Country is read from `CF-IPCountry`, with `X-Country-Code` as a compatibility
fallback. Only use these headers when a trusted reverse proxy strips client
supplied values and writes the country value; the API does not perform a GeoIP
lookup.

Example configuration:

```json
{
  "ShortenLink": {
    "Analytics": {
      "Enabled": true,
      "UseAsyncWorker": true,
      "QueueCapacity": 512
    }
  }
}
```

## Admin CSV Export

The protected short-link workspace includes an `Export CSV` action beside the
discovery filters. It reuses the current search, status, folder, tag, and sort
criteria, fetches all matching pages through `GET /api/short-links`, and
downloads a stable UTF-8 CSV containing only safe link metadata, including
folder and tags. Ownership, sharing, and
permission scope remain enforced by the API; the browser does not broaden the
result set.

## Short-Link QR Codes

Authorized rows in the protected short-link workspace expose a `QR code`
action. The dialog generates a PNG QR image from the existing public `shortUrl`,
shows the URL for confirmation, and supports downloading the image. No
destination URL, credential, audit field, or session material is encoded, and
QR presentation does not add an endpoint or change redirect behavior. QR
generation is client-side and remains scoped to the links already returned by
the authorized list API.

## Mutation Audit Log

Successful short-link create, update, activate, deactivate, delete, share grant,
share update, and share revoke operations append durable audit events. Successful
login, token refresh, user-owned API-key changes, and admin user, role,
permission-override, and persisted security-assignment changes are audited
through the same contract. Events retain the actor, action, typed stable target,
owner when user visibility applies, outcome, occurrence time, and optional
non-secret context; destination URLs, passwords, raw API keys, credential
hashes, and session tokens are not recorded.

Audit delivery is fail-open: the business transaction commits first, then the
event is queued for background persistence through a separate database scope.
Temporary audit storage failures are logged and do not fail link, sharing,
authentication, or security-administration requests; audit discovery is
eventually consistent with the completed mutation.

Authorized callers with `audit_logs.read` can query:

```bash
curl --get http://localhost:5000/api/audit-logs \
  --data-urlencode 'limit=50' \
  --data-urlencode 'cursor=<cursor>' \
  --data-urlencode 'fe=((Action eq `short_link.updated`) & (TargetId eq `abc1234`) & (ActorId eq `user-1`) & (OccurredAt ge `2026-07-01T00:00:00Z`) & (OccurredAt le `2026-07-31T23:59:59Z`))'
```

Audit filters allow `ActorId`, `Action`, `TargetType`, `TargetId`, `Outcome`,
`OccurredAt`, and `SubjectUserId`. HTTP clients must URL-encode the `fe` value.

Results are newest-first and return `{ "items": [...], "nextCursor": "..." }`.
Admin can inspect all matching events. User results remain limited to events for
owned links, links currently shared with that user, and that user's own
authentication and API-key activity. Security-administration events remain
Admin-only even when they target that user. The persisted owner id keeps owner
history available after link deletion. Missing credentials use the existing
`401 unauthorized` response, and callers without `audit_logs.read` receive the
existing `403 forbidden` response.

Authenticated callers with `audit_logs.read` can open `/audit-logs` in the web
app. The investigation page loads newest-first results, supports action,
target-id, actor-id, and time-range filters, and follows the opaque cursor to
load older events. Scope is always enforced by the API; the browser does not
reconstruct or broaden Admin/User visibility.

## Redirect Cache

Phase 3 also adds an opt-in cache path for successful redirects:

- Leave `ShortenLink:Cache:Enabled` as `false` to keep redirect lookups database-backed.
- Set `ShortenLink:Cache:Enabled` to `true` and `ShortenLink:Cache:Provider` to `Memory` for a local in-process cache.
- Set `ShortenLink:Cache:Provider` to `Redis` and provide `ShortenLink:Cache:RedisConnectionString` to use Redis without changing application code.
- `ShortenLink:Cache:EntryTtlSeconds` controls cache duration for links that do not have their own expiration.
- Deactivating, activating, updating, or deleting a link invalidates its cache entry so previously cached redirects stop resolving. Tenant-aware providers partition both lookup and invalidation keys.

Example memory-cache configuration:

```json
{
  "ShortenLink": {
    "Cache": {
      "Enabled": true,
      "Provider": "Memory",
      "RedisConnectionString": "localhost:6379",
      "EntryTtlSeconds": 3600
    }
  }
}
```

## Endpoint Rate Limiting

Phase 3 adds opt-in HTTP rate limiting for public create and redirect paths:

- Leave `ShortenLink:RateLimiting:Enabled` as `false` to keep current endpoint behavior.
- Set `ShortenLink:RateLimiting:Enabled` to `true` to apply independent fixed-window limits to create and redirect requests.
- `ShortenLink:RateLimiting:Create` applies to `POST /api/short-links`.
- `ShortenLink:RateLimiting:Redirect` applies to `GET /{code}` before cache lookup, database lookup, or click analytics recording.
- Over-limit requests return HTTP `429`.

Admins can inspect the current rate-limit configuration and bounded recent
throttling activity through `GET /api/admin/rate-limits`. The response reports
whether rate limiting is enabled, each policy's permit limit, fixed-window
duration, queue limit, aggregate rejection count, and the most recent
policy/timestamp pairs. It intentionally omits IP addresses, URLs, short-link
codes, request payloads, and credentials. Activity is process-local and bounded
in memory; it is operational visibility, not a durable metrics store. Non-Admin
callers receive `403 forbidden`.

Example configuration:

```json
{
  "ShortenLink": {
    "RateLimiting": {
      "Enabled": true,
      "Create": {
        "PermitLimit": 60,
        "WindowSeconds": 60,
        "QueueLimit": 0
      },
      "Redirect": {
        "PermitLimit": 120,
        "WindowSeconds": 60,
        "QueueLimit": 0
      }
    }
  }
}
```

Example Redis configuration:

```json
{
  "ShortenLink": {
    "Cache": {
      "Enabled": true,
      "Provider": "Redis",
      "RedisConnectionString": "localhost:6379",
      "EntryTtlSeconds": 3600
    }
  }
}
```

## Demo API Swagger And Demo UI

The demo API uses Swashbuckle for development-time Swagger/OpenAPI.

```powershell
dotnet run --project src\ShortenLink.Api\ShortenLink.Api.csproj --launch-profile https
```

Open:

```text
https://localhost:7154/swagger
```

The API now exposes:

- `POST /api/short-links`
- `GET /api/short-links/{code}`
- `GET /api/short-links/{code}/analytics`
- `DELETE /api/short-links/{code}`
- `GET /{code}`
- `GET /api/health`

In development, `src\ShortenLink.Api\appsettings.Development.json` overrides `ShortenLink:BaseUrl` to `https://localhost:7154` and sets `ShortenLink:Redirect:FrontendFallbackPath` to `http://localhost:5173/not-found` so returned short URLs and unknown-code fallback both line up with the local split API + Vite setup.

## Frontend Demo

### Expiry Presentation

The short-link list and detail view display expiry values with the browser's
local timezone abbreviation. Active links expiring within the next 24 hours
also show an explicit `Expiring soon` label and supporting text; inactive and
expired states keep their existing lifecycle meaning. This is presentation
only: the API still requires a future expiry and remains authoritative for
status, authorization, and redirect behavior.

Create and Edit share the same local-time quick picks: `+30m`, `+60m`,
`+180m`, `+6h`, and `+12h`. The selected local value is converted to the UTC
instant sent to the API; no fixed timezone is imposed on the browser.

The React + Vite app provides authenticated creation, compact copy feedback,
owned/shared link management, Admin dashboard and security screens, detail,
analytics, lifecycle actions, and fallback/status experiences. `/not-found`
remains compatible with the configured unknown-code fallback.

Start the API in one terminal:

```powershell
dotnet run --project src\ShortenLink.Api\ShortenLink.Api.csproj --launch-profile https
```

The `https` launch profile keeps both `https://localhost:7154` and `http://localhost:5188` available, so the returned short URLs and the Vite proxy target both work during local development and smoke runs.

Then start the frontend in another:

```powershell
cd .\src\ShortenLink.Web
npm install
npm run dev
```

Open:

```text
http://localhost:5173
```

For the fresh local database, sign in with:

```text
Email: admin@shortenlink.local
Password: admin
```

The Vite dev server proxies `/api/*` requests to `http://localhost:5188` by default. Override that target when needed:

```powershell
$env:SHORTENLINK_API_PROXY_TARGET = "http://localhost:5188"
npm run dev
```

For a production-style frontend build:

```powershell
cd .\src\ShortenLink.Web
npm run build
```

Dependencies are not vendored in this repo.

## PostgreSQL Notes

Phase 2 adds configuration-driven provider selection to the reusable library boundary:

- Leave `ShortenLink:Database:UsePostgres` as `false` to keep SQLite as the default provider.
- Set `ShortenLink:Database:UsePostgres` to `true` and provide `ShortenLink:Database:PostgresConnectionString` to switch the same host and library code to PostgreSQL.
- The reusable API, repository, and service contracts do not change between providers.
- `dotnet pack ShortenLink.slnx -c Release` still produces the same reusable packages; provider choice stays in configuration.

### UTC Timestamp Storage

Public and domain contracts continue to use `DateTimeOffset`, while the EF
persistence boundary converts timestamps to UTC `DateTime` values. This keeps
SQLite range/order predicates index-friendly and maps to PostgreSQL
`timestamp with time zone` without provider-specific behavior in application
code.

On SQLite startup, the built-in host performs an idempotent compatibility pass
that converts legacy timestamp strings containing `Z` or an explicit offset to
the UTC storage format. Custom hosts that initialize an existing SQLite schema
themselves should call:

```csharp
await ShortLinkDatabaseSchema.EnsureUtcTimestampSchemaAsync(dbContext, cancellationToken);
```

Fresh databases already write the normalized format and do not require a data
rewrite.

### Local PostgreSQL Host Smoke

Minimum prerequisites:

- A reachable PostgreSQL instance.
- A database and credentials that match your connection string.
- TCP access to the PostgreSQL host and port from this machine.

Example PowerShell environment override:

```powershell
$env:ShortenLink__Database__UsePostgres = "true"
$env:ShortenLink__Database__PostgresConnectionString = "Host=localhost;Port=5432;Database=shorten_link;Username=postgres;Password=postgres"
dotnet run --project src\ShortenLink.Api\ShortenLink.Api.csproj --no-launch-profile
```

For a repeatable host smoke run, use:

```powershell
.\scripts\smoke-postgres-host.ps1
```

Override the connection string or API URL when needed:

```powershell
.\scripts\smoke-postgres-host.ps1 -ConnectionString "Host=localhost;Port=5432;Database=shorten_link;Username=postgres;Password=postgres" -ApiUrl "http://127.0.0.1:5199"
```

The smoke script:

- checks that PostgreSQL is reachable before starting the API
- runs the demo host with `UsePostgres = true`
- verifies health, create, detail, redirect, and deactivate behavior
- returns a JSON summary on success

When PostgreSQL is not reachable, the script fails early with a concrete blocker message instead of pretending the host smoke passed.

## Local Operational Stack With Docker Compose

Phase 3 now includes an optional Docker Compose path for the demo API plus its operational dependencies:

- PostgreSQL for the configured database provider
- Redis for redirect cache
- async click analytics enabled by configuration
- endpoint rate limiting enabled by configuration

This stack is optional. The default non-Docker SQLite developer flow still works exactly as before.

### Start The Stack

From the repository root:

```powershell
docker compose up -d --build
```

The composed stack exposes:

- API: `http://localhost:5188`
- PostgreSQL: `localhost:5432`
- Redis: `localhost:6379`

The compose path configures the API with environment variables only:

- `ShortenLink__Database__UsePostgres=true`
- `ShortenLink__Database__PostgresConnectionString=Host=postgres;Port=5432;Database=shorten_link;Username=postgres;Password=postgres`
- `ShortenLink__Cache__Enabled=true`
- `ShortenLink__Cache__Provider=Redis`
- `ShortenLink__Cache__RedisConnectionString=redis:6379`
- `ShortenLink__Analytics__Enabled=true`
- `ShortenLink__Analytics__UseAsyncWorker=true`
- `ShortenLink__RateLimiting__Enabled=true`

That keeps provider selection and operational behavior in configuration instead of application code.

### Stop The Stack

```powershell
docker compose down
```

To remove the PostgreSQL and Redis volumes as well:

```powershell
docker compose down -v
```

### Smoke Check The Stack

For a repeatable compose-backed smoke run:

```powershell
.\scripts\smoke-docker-compose.ps1
```

The script:

- validates the compose file with `docker compose config`
- starts the stack with `docker compose up -d --build`
- waits for `GET /api/health`
- verifies create, redirect, detail, and deactivate behavior
- shuts the stack down with `docker compose down -v`
- returns a JSON summary on success

Keep the stack running after the smoke when needed:

```powershell
.\scripts\smoke-docker-compose.ps1 -KeepRunning
```

Point the smoke script at a different compose file or API URL when needed:

```powershell
.\scripts\smoke-docker-compose.ps1 -ComposeFile .\compose.yml -ApiUrl http://127.0.0.1:5188
```

### Default SQLite Path Still Works

Docker Compose does not replace the default local path. Outside Docker, leave:

```json
{
  "ShortenLink": {
    "Database": {
      "UsePostgres": false,
      "SqliteConnectionString": "Data Source=shorten-link.db"
    }
  }
}
```

Then continue using the normal local host flow:

```powershell
dotnet run --project src\ShortenLink.Api\ShortenLink.Api.csproj --launch-profile https
```
