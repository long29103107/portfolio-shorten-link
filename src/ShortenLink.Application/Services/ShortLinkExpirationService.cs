using ShortenLink.Core;
using ShortenLink.Core.Abstractions;
using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Core.Events;
using ShortenLink.Core.Exceptions;
using ShortenLink.Core.Services;

namespace ShortenLink.Application.Services;

public sealed class ShortLinkExpirationService(
    IShortLinkRepository repository,
    IShortLinkExpirationEvaluator evaluator,
    IShortLinkExpirationEventSink? eventSink = null)
    : IShortLinkExpirationService
{
    public async Task<ShortLinkExpirationBatchResult> EvaluateBatchAsync(
        ShortLinkExpirationBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!ShortLinkTenantId.IsValid(request.TenantId))
        {
            throw new RequestValidationException(
                ShortLinkErrorCodes.InvalidTenantId,
                $"Tenant identifier must be at most {ShortLinkTenantId.MaxLength} characters.");
        }

        if (!ShortLinkExpirationEvaluator.TryDecodeCursor(
                request.Cursor,
                out var beforeExpiresAt,
                out var beforeCode))
        {
            throw new RequestValidationException(
                ErrorCodes.InvalidCursor,
                "Expiration cursor is invalid.");
        }

        if (repository is not IShortLinkExpirationRepository expirationRepository)
        {
            throw new BusinessRuleException(
                ShortLinkErrorCodes.TenantNotSupported,
                "The configured persistence provider does not support expiration batches.");
        }

        var tenantId = ShortLinkTenantId.Normalize(request.TenantId);
        var limit = Math.Clamp(request.Limit, 1, ShortLinkExpirationEvaluator.MaxLimit);
        var candidates = await expirationRepository.ListExpirationCandidatesAsync(
            tenantId,
            beforeExpiresAt,
            beforeCode,
            limit + 1,
            cancellationToken);
        var result = evaluator.Evaluate(
            candidates,
            request with
            {
                TenantId = tenantId,
                Cursor = null,
                Limit = limit
            });

        if (eventSink is not null)
        {
            foreach (var item in result.Items.Where(
                         item => item.Outcome == ShortLinkExpirationOutcome.Expired))
            {
                try
                {
                    _ = eventSink.TryPublish(
                        ShortLinkExpirationEvent.FromEvaluation(item),
                        cancellationToken);
                }
                catch
                {
                    // Expiration hooks are opt-in and fail-open; evaluation is read-only.
                }
            }
        }

        return result;
    }
}
