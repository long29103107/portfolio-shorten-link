# Phase 030_005 Dependency Direction and Duplication Audit

Date: 2026-08-07

## Decision

Keep the current project dependency graph and do not extract another shared
package or merge the remaining similarly-shaped options/serializer helpers.
The graph is acyclic, and the remaining repetitions belong to different
ownership boundaries.

## Verified dependency graph

| Project | References |
|---|---|
| `ShortenLink.Core` | none |
| `ShortenLink.Auditing` | none |
| `ShortenLink.Mediator` | none |
| `ShortenLink.Messaging` | none |
| `ShortenLink.Application` | Core, Auditing, Mediator |
| `ShortenLink.Infrastructure` | Core, Auditing |
| `ShortenLink.Hosting` | Core, Application, Infrastructure, Auditing, Mediator, Messaging |
| `ShortenLink.Api` | Hosting, Application, Mediator |

The Hosting project is the composition boundary: it wires application,
persistence, and messaging providers. Core, Application, and the reusable
provider-neutral packages do not reference Hosting or the API. No project
reference cycle is present.

## Duplication decisions

- `MessageQueueOptions` remains in `ShortenLink.Messaging` because it is the
  generic provider-neutral factory contract.
- `ShortenLinkQueueOptions` remains in Hosting because it owns host-specific
  queue names and separate audit/analytics capacities. Merging these types
  would make the reusable messaging package depend on host concerns.
- Module-local `JsonSerializerOptions` instances remain local to persistence,
  caching, token, and queue boundaries. A shared serializer service would add
  lifetime/configuration coupling without reducing a behaviorally meaningful
  duplication.
- No second contracts package is introduced; the earlier contract-boundary
  decision remains valid because Core is already the stable shared contract
  location.

## Guard

Future backend refactors must preserve these constraints:

1. Core and provider-neutral packages must not reference Hosting, API, or
   Infrastructure.
2. Application must not reference Hosting, API, or Infrastructure.
3. Infrastructure may depend on Core and provider-neutral auditing contracts,
   but not on Application or Hosting.
4. New shared libraries require at least two legitimate consumers or an
   external-host contract with independent behavior characterization.

Verification for this audit used the project-reference graph and the full
solution build/test suite; no production behavior change was required.
