using ShortenLink.Core.Events;

namespace ShortenLink.Core.Abstractions;

public interface IShortLinkExpirationEventSink
{
    bool TryPublish(
        ShortLinkExpirationEvent @event,
        CancellationToken cancellationToken = default);
}
