using ShortenLink.Core.Events;

namespace ShortenLink.Core.Abstractions;

/// <summary>
/// Non-blocking, opt-in delivery boundary for lifecycle and redirect events.
/// Implementations should enqueue or hand off work and return promptly. A
/// false result means the sink did not accept the event; business operations
/// must remain successful regardless of sink availability.
/// </summary>
public interface IShortLinkEventSink
{
    bool TryPublish(
        ShortLinkLifecycleEvent @event,
        CancellationToken cancellationToken = default);
}
