using ShortenLink.Core.Contracts.Expiration;
using ShortenLink.Core.Domain;

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
