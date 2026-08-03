using Microsoft.Extensions.Logging;
using ShortenLink.Core.Services;
using ShortenLink.Messaging;

namespace ShortenLink.Hosting;

internal sealed class MessageQueueShortLinkClickRecorder(
    IMessageQueue<RecordShortLinkClickRequest> queue,
    ILogger<MessageQueueShortLinkClickRecorder> logger) : IShortLinkClickRecorder
{
    public async Task RecordAsync(
        RecordShortLinkClickRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = await queue.PublishAsync(request, cancellationToken);
        if (result == QueuePublishResult.Dropped)
        {
            logger.LogWarning(
                "Short-link click analytics queue is full. Dropping click event for code {ShortCode}.",
                request.ShortCode);
        }
    }
}
