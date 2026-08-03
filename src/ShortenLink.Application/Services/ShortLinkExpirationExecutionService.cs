using ShortenLink.Core;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Services;

namespace ShortenLink.Application.Services;

public sealed class ShortLinkExpirationExecutionService(
    IShortLinkExpirationService expirationService,
    IShortLinkExpirationCheckpointRepository checkpointRepository,
    IShortLinkExpirationCacheInvalidationSink cacheInvalidationSink,
    TimeProvider timeProvider)
    : IShortLinkExpirationExecutionService
{
    public async Task<ShortLinkExpirationExecutionResult> ExecuteBatchAsync(
        ShortLinkExpirationExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ShortLinkTenantId.IsValid(request.TenantId))
        {
            throw new RequestValidationException(
                ShortLinkErrorCodes.InvalidTenantId,
                $"Tenant identifier must be at most {ShortLinkTenantId.MaxLength} characters.");
        }

        var tenantId = ShortLinkTenantId.Normalize(request.TenantId);
        var checkpoint = request.ResumeFromCheckpoint
            ? await checkpointRepository.FindAsync(tenantId, cancellationToken)
            : null;
        var previousCursor = checkpoint?.Cursor;
        var batch = await expirationService.EvaluateBatchAsync(
            new ShortLinkExpirationBatchRequest(
                request.EvaluatedAtUtc,
                tenantId,
                previousCursor,
                request.Limit,
                request.RetentionPolicy),
            cancellationToken);

        var cacheInvalidationHandoffs = 0;
        foreach (var evaluation in batch.Items.Where(item => item.CacheInvalidationRequired))
        {
            if (!await cacheInvalidationSink.TryInvalidateAsync(evaluation, cancellationToken))
            {
                throw new BusinessRuleException(
                    ShortLinkErrorCodes.ExpirationHandoffUnavailable,
                    "The expiration cache invalidation handoff was not accepted.");
            }

            cacheInvalidationHandoffs++;
        }

        await checkpointRepository.SaveAsync(
            new ShortLinkExpirationCheckpoint(
                tenantId,
                batch.NextCursor,
                request.EvaluatedAtUtc,
                timeProvider.GetUtcNow()),
            cancellationToken);

        return new ShortLinkExpirationExecutionResult(
            batch,
            previousCursor,
            batch.NextCursor,
            true,
            cacheInvalidationHandoffs);
    }
}
