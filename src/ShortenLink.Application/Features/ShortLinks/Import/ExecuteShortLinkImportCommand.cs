using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Requests;
using ShortenLink.Core.Contracts.Responses;
using ShortenLink.Core.Security;
using ShortenLink.Core.Services;
using ShortenLink.Mediator;

namespace ShortenLink.Application.Features.ShortLinks.Import;

public sealed record ExecuteShortLinkImportCommand(
    IReadOnlyList<ShortLinkImportItemRequest>? Items) : IRequest<ShortLinkImportExecutionResponse>;

internal sealed class ExecuteShortLinkImportCommandHandler(
    IShortLinkImportValidator validator,
    IShortLinkService shortLinkService,
    ICurrentRequestContext requestContext,
    ShortLinkAuditWriter auditWriter)
    : IRequestHandler<ExecuteShortLinkImportCommand, ShortLinkImportExecutionResponse>
{
    public async Task<ShortLinkImportExecutionResponse> Handle(
        ExecuteShortLinkImportCommand request,
        CancellationToken cancellationToken)
    {
        var actor = await requestContext.AuthorizeAsync(
            ShortenLinkPermissionCatalog.ShortLinksImport,
            cancellationToken);
        var creator = await requestContext.GetCurrentUserAsync(cancellationToken);
        var items = request.Items ?? [];
        var results = new List<ShortLinkImportItemResponse>();
        var replayedCount = 0;

        await foreach (var validation in validator.ValidateAsync(
            ToAsyncEnumerable(items),
            cancellationToken))
        {
            if (!validation.Result.Succeeded)
            {
                results.Add(validation.Result);
                continue;
            }

            CreateShortLinkResponse createResult;
            try
            {
                createResult = await shortLinkService.CreateAsync(
                    new CreateShortLinkRequest(
                        validation.Item.OriginalUrl!,
                        validation.Item.ExpiredAtUtc,
                        creator?.UserId,
                        creator?.DisplayName,
                        creator?.Username,
                        validation.Item.IdempotencyKey,
                        actor.TenantId,
                        validation.Item.ActiveFromUtc),
                    cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch
            {
                results.Add(new ShortLinkImportItemResponse(
                    validation.ItemNumber,
                    false,
                    ShortLinkImportErrorCodes.PersistenceFailure,
                    "The item could not be persisted."));
                continue;
            }

            if (!createResult.Succeeded || createResult.ShortLink is null)
            {
                results.Add(new ShortLinkImportItemResponse(
                    validation.ItemNumber,
                    false,
                    createResult.ErrorCode ?? ShortLinkImportErrorCodes.PersistenceFailure,
                    createResult.ErrorMessage ?? "The item could not be persisted."));
                continue;
            }

            if (!createResult.Replayed)
            {
                await auditWriter.RecordAsync(
                    actor,
                    ShortLinkAuditActions.Created,
                    createResult.ShortLink.Code,
                    createResult.ShortLink.CreatedByUserId,
                    cancellationToken: cancellationToken);
            }
            else
            {
                replayedCount++;
            }

            results.Add(new ShortLinkImportItemResponse(
                validation.ItemNumber,
                true,
                ShortCode: createResult.ShortLink.Code,
                Replayed: createResult.Replayed));
        }

        var succeededCount = results.Count(static result => result.Succeeded);
        return new ShortLinkImportExecutionResponse(
            results.Count,
            succeededCount,
            results.Count - succeededCount,
            replayedCount,
            items.Count > ShortLinkImportLimits.MaxDryRunItems,
            results);
    }

    private static async IAsyncEnumerable<ShortLinkImportItemRequest> ToAsyncEnumerable(
        IReadOnlyList<ShortLinkImportItemRequest> items)
    {
        foreach (var item in items)
        {
            yield return item;
            await Task.Yield();
        }
    }
}
