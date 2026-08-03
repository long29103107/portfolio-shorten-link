using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Events;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkExpirationEvaluator
{
    ShortLinkExpirationBatchResult Evaluate(
        IReadOnlyList<ShortLink> candidates,
        ShortLinkExpirationBatchRequest request);
}

public interface IShortLinkExpirationService
{
    Task<ShortLinkExpirationBatchResult> EvaluateBatchAsync(
        ShortLinkExpirationBatchRequest request,
        CancellationToken cancellationToken = default);
}

public interface IShortLinkExpirationEventSink
{
    bool TryPublish(
        ShortLinkExpirationEvent @event,
        CancellationToken cancellationToken = default);
}
