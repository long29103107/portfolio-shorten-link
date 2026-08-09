---
phase: 039
task: 039_001
title: Bulk Operations
status: done
created_at: 2026-08-09
started_at: 2026-08-09
completed_at: 2026-08-09
depends_on:
  - 038_001
---

# 039_001 - Bulk Operations

## Step Goal

Replace the management UI's per-link bulk calls with a bounded bulk contract
that can lifecycle-manage selected links, assign or clear folder/tags, and
export the selected rows while keeping per-item authorization and failures
visible.

## Dependency

- Phase 038's normalized folder/tag metadata and discovery filtering.
- Existing single-link lifecycle, update, delete, authorization, audit, and
  CSV export flows.

## Scope

In:

- `POST /api/short-links/bulk` with activate, deactivate, delete, and organize
  operations.
- Request validation for bounded, unique codes and normalized organization
  metadata.
- Per-item success/failure response and existing permission/access semantics.
- React bulk lifecycle actions, organization dialog, selected-row CSV export,
  loading states, and tests.
- README/API documentation.

Out:

- Bulk import, background jobs, cross-page selection, and bulk sharing.

## Relevant Standards

- `.okf/standards/architecture.md`
- `.okf/standards/coding-style.md`
- `.okf/standards/api-design.md`
- `.okf/standards/testing.md`
- `PRODUCT_VISION.md`

## Affected Files

- `src/ShortenLink.Core/Contracts/Requests`
- `src/ShortenLink.Core/Abstractions/Services/Application/IShortLinkService.cs`
- `src/ShortenLink.Application/Features/ShortLinks/Bulk`
- `src/ShortenLink.Application/Services/ShortLinkService.Operations.cs`
- `shared/ShortenLink.Hosting/Endpoints/Map.cs`
- `src/ShortenLink.Web/src/features/short-links`
- `tests/ShortenLink.Api.Tests`
- `tests/ShortenLink.Core.Tests`
- `README.md`

## Acceptance Criteria

- The bulk endpoint accepts 1-100 unique codes and one supported operation.
- Activate/deactivate require status permission plus per-link Edit access;
  delete requires delete permission plus owner/Admin access; organization
  requires update permission plus per-link Edit access.
- Organization updates can assign or clear one folder and normalized tags,
  including links whose expiry has already passed.
- A successful item is applied once and represented in the response; an
  individual missing or unauthorized item is reported as a failure without
  hiding successful sibling results.
- The UI sends one bulk request for lifecycle/organization changes, confirms
  destructive operations, resets selection after success, and can download
  selected rows as CSV.
- Backend/API/frontend tests and builds pass, and README documents the bulk
  contract.

## Foundation for Next Step

The task leaves a bounded, authorization-aware bulk contract and selected-row
export path that can later be moved behind an asynchronous job boundary without
changing the operation vocabulary or item result shape.

## Verification

- `dotnet build ShortenLink.slnx --no-restore`
- `dotnet test ShortenLink.slnx --no-build --no-restore`
- `cd src/ShortenLink.Web; bun test`
- `cd src/ShortenLink.Web; bun run build`
- `git diff --check`

## Done Notes

Implemented `POST /api/short-links/bulk` with bounded unique code validation,
activate/deactivate/delete/organize operations, per-item authorization, and
partial result reporting. Organization updates preserve all existing link
properties and can update expired links without weakening the normal update
expiration rules. The React management table now sends one bulk request for
lifecycle and organization actions, supports selected-row CSV export, confirms
destructive actions, and exposes a folder/tag organization dialog.

Verification passed:

- `dotnet build ShortenLink.slnx --no-restore`
- `dotnet test ShortenLink.slnx --no-build --no-restore` (257 tests)
- `dotnet test tests/ShortenLink.Api.Tests/ShortenLink.Api.Tests.csproj --no-restore --filter FullyQualifiedName~BulkOperations`
- `bun test` (84 tests)
- `bun run build`
- `git diff --check` (exit code 0; only Git line-ending warnings)
