using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using ShortenLink.Core.Services;

namespace ShortenLink.Hosting;

internal sealed class ChannelShortLinkClickRecorder : IShortLinkClickRecorder
{
    private readonly ChannelWriter<RecordShortLinkClickRequest> writer;
    private readonly ILogger<ChannelShortLinkClickRecorder> logger;

    public ChannelShortLinkClickRecorder(
        Channel<RecordShortLinkClickRequest> channel,
        ILogger<ChannelShortLinkClickRecorder> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);

        writer = channel.Writer;
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task RecordAsync(
        RecordShortLinkClickRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!writer.TryWrite(request))
        {
            logger.LogWarning(
                "Short-link click analytics queue is full. Dropping click event for code {ShortCode}.",
                request.ShortCode);
        }

        return Task.CompletedTask;
    }
}
