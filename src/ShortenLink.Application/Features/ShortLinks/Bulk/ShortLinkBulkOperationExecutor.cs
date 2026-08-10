using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Core;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Responses;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Security;
using ShortenLink.Core.Services;

namespace ShortenLink.Application.Features.ShortLinks.Bulk;

public sealed class ShortLinkBulkOperationExecutor(
    IShortLinkService shortLinkService,
    IShortLinkShareRepository shareRepository,
    ShortLinkAccessGuard accessGuard,
    ShortLinkAuditWriter auditWriter)
{
    public async Task<ShortLinkBulkOperationResponse> ExecuteAsync(
        ExecuteShortLinkBulkOperationCommand request,
        CurrentRequestActor actor,
        CancellationToken cancellationToken = default,
        Action<int>? onProcessed = null)
    {
        var operation = ShortLinkBulkOperations.Normalize(request.Operation);
        var items = new List<ShortLinkBulkOperationItemResponse>(request.Codes!.Count);

        foreach (var rawCode in request.Codes)
        {
            var code = rawCode.Trim();
            try
            {
                var existing = ShortLinkFeatureSupport.GetRequired(
                    await shortLinkService.GetDetailsAsync(code, cancellationToken));
                await accessGuard.EnsureAccessAsync(
                    existing,
                    actor,
                    ShortLinkShareAccess.Edit,
                    operation == ShortLinkBulkOperations.Delete,
                    operation == ShortLinkBulkOperations.Delete
                        ? "Only the owner or an admin can delete this short link."
                        : "Edit access is required for this short link.",
                    cancellationToken);

                switch (operation)
                {
                    case ShortLinkBulkOperations.Activate:
                        ShortLinkFeatureSupport.EnsureSucceeded(await shortLinkService.ActivateAsync(code, cancellationToken));
                        await auditWriter.RecordAsync(actor, ShortLinkAuditActions.Activated, existing.Code, existing.CreatedByUserId, cancellationToken: cancellationToken);
                        break;
                    case ShortLinkBulkOperations.Deactivate:
                        ShortLinkFeatureSupport.EnsureSucceeded(await shortLinkService.DeactivateAsync(code, cancellationToken));
                        await auditWriter.RecordAsync(actor, ShortLinkAuditActions.Deactivated, existing.Code, existing.CreatedByUserId, cancellationToken: cancellationToken);
                        break;
                    case ShortLinkBulkOperations.Delete:
                        ShortLinkFeatureSupport.EnsureSucceeded(await shortLinkService.DeleteAsync(code, cancellationToken));
                        await shareRepository.DeleteByShortCodeAsync(code, cancellationToken);
                        await auditWriter.RecordAsync(actor, ShortLinkAuditActions.Deleted, existing.Code, existing.CreatedByUserId, cancellationToken: cancellationToken);
                        break;
                    case ShortLinkBulkOperations.Organize:
                        ShortLinkFeatureSupport.GetRequired(await shortLinkService.UpdateOrganizationAsync(code, request.Folder, request.Tags, cancellationToken));
                        await auditWriter.RecordAsync(actor, ShortLinkAuditActions.Updated, existing.Code, existing.CreatedByUserId, detail: "bulk_organization", cancellationToken: cancellationToken);
                        break;
                }

                items.Add(new ShortLinkBulkOperationItemResponse(code, true));
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (ShortenLinkException exception)
            {
                items.Add(new ShortLinkBulkOperationItemResponse(code, false, exception.ErrorCode, exception.Message));
            }
            onProcessed?.Invoke(items.Count);
        }

        var succeededCount = items.Count(static item => item.Succeeded);
        return new ShortLinkBulkOperationResponse(operation, items.Count, succeededCount, items.Count - succeededCount, items);
    }
}
