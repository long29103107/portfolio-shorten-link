# NuGet.org Publish Preflight Checklist

Date: 2026-07-15

This checklist is the final maintainer gate before any intentional live
NuGet.org publish attempt for the reusable Shorten Link packages.

The repository can prove package build, metadata, dependency shape, local feed
rehearsal, and clean consumer installation. It cannot prove NuGet.org account
access, package ID ownership, API key scope, organizational approval, or whether
a selected package version should be made public. Those facts must be confirmed
outside source control before running `scripts\publish-nuget.ps1 -Publish`.

## Packages In Scope

Review all three reusable packages as one release set:

| Package ID | Publish role | Required review |
|---|---|---|
| `ShortenLink.Core` | Domain contracts, validation, service abstractions, requests, and results. | Confirm the package ID is owned or available on NuGet.org, the version is correct, and the public contract is intended for release. |
| `ShortenLink.Infrastructure` | Persistence and provider adapters. | Confirm it depends on the matching `ShortenLink.Core` version and that its package ID and version are owned or available. |
| `ShortenLink.Hosting` | Normal ASP.NET Core host entry point. | Confirm it depends on the matching lower-level package versions and is the package consumers should normally install. |

`ShortenLink.Api` and `ShortenLink.Web` are demo applications and must not be
published as reusable NuGet packages.

## Local Repository Gates

Run these commands from the repository root before considering a live publish:

```powershell
dotnet build ShortenLink.slnx --verbosity minimal
dotnet test ShortenLink.slnx --verbosity minimal
dotnet pack ShortenLink.slnx -c Release --verbosity minimal
.\scripts\release-dry-run.ps1 -PackageVersion <version>
.\scripts\rehearse-local-feed.ps1 -PackageVersion <version> -ResetFeed
.\scripts\smoke-consumer-package.ps1 -PackageVersion <version>
```

Required local evidence:

- Build, tests, and pack complete without repository regressions.
- `release-dry-run.ps1` reports `Published: false` and validates package
  metadata, README inclusion, license expression, repository metadata, tags,
  dependencies, assemblies, and absence of demo API/Web coupling.
- Local feed rehearsal copies `ShortenLink.Core`, `ShortenLink.Infrastructure`,
  and `ShortenLink.Hosting` into a local folder feed without using
  NuGet.org credentials.
- Consumer smoke installs `ShortenLink.Hosting` into a clean ASP.NET Core
  app and verifies create, detail, redirect, deactivate, and post-delete
  redirect behavior.

Do not replace these gates with a real NuGet.org push. A live push is only a
manual maintainer action after both local evidence and external prerequisites
are satisfied.

## External NuGet.org Prerequisites

Before publishing, a maintainer must confirm each item below outside the
repository:

- A responsible maintainer is signed in to the correct NuGet.org account.
- The account or organization owns, reserves, or is allowed to create all three
  package IDs: `ShortenLink.Core`, `ShortenLink.Infrastructure`, and
  `ShortenLink.Hosting`.
- The chosen `<version>` has not already been published for any of the three
  package IDs, unless this is a deliberate retry using `-SkipDuplicate`.
- The API key is scoped only as broadly as needed for the intended package IDs
  and push operation.
- The API key is supplied from an environment variable, secret store, or
  interactive secure source. It must not be committed, logged, pasted into docs,
  or added to project files.
- The package owner or organization, release notes, license, repository URL,
  tags, and package visibility are approved.
- The maintainer with release authority has approved the publish window and
  understands that NuGet package versions are immutable after publication.

## Credential Handling

Use one of these approaches only at publish time:

```powershell
$env:NUGET_API_KEY = "<set outside source control>"
.\scripts\publish-nuget.ps1 -PackageVersion <version> -Publish
```

or:

```powershell
.\scripts\publish-nuget.ps1 -PackageVersion <version> -Publish -NuGetApiKey "<read from a secret store>"
```

The API key value must come from a secure source controlled by the maintainer.
Do not commit it, put it in `.env` files, paste it into task files, or add it to
CI variables unless a separate approved automation task explicitly scopes that
change.

Preview the command without publishing:

```powershell
.\scripts\publish-nuget.ps1 -PackageVersion <version>
```

The preview path fails closed and reports that `-Publish` plus credentials are
required before any package push.

## Go / No-Go Decision

Proceed only when every local gate passes and every external prerequisite is
confirmed.

| Condition | Decision |
|---|---|
| Package ID ownership or availability is unknown. | No-go. Stop and resolve ownership before creating a publish task. |
| The NuGet.org account or organization is wrong. | No-go. Stop and switch to the approved account or organization. |
| The API key is missing, over-scoped, expired, or stored insecurely. | No-go. Create or retrieve a correctly scoped key outside source control. |
| Maintainer approval is missing. | No-go. Stop; do not publish on behalf of the maintainer. |
| The version already exists unexpectedly. | No-go. Choose a new version or investigate whether `-SkipDuplicate` is an intentional retry. |
| Local build, test, pack, dry-run, rehearsal, or smoke fails. | No-go. Fix the repository issue and rerun all required gates. |
| All local gates pass and all external prerequisites are confirmed. | Go. A maintainer may run the manual publish command intentionally. |

## Post-Publish Checks

After a successful intentional publish:

- Verify all three packages are visible on NuGet.org under the expected owner.
- Confirm package descriptions, README, license, repository metadata, tags, and
  dependency versions render correctly.
- Install `ShortenLink.Hosting` into a clean consumer app from NuGet.org and
  rerun the create, detail, redirect, and deactivate smoke flow.
- If a bad package is published, prefer deprecating or unlisting the affected
  version and publishing a corrected version. Do not try to overwrite the same
  NuGet version.
