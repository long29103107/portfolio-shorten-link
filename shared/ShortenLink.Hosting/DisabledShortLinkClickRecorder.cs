using ShortenLink.Core.Services;

namespace ShortenLink.Hosting;

internal sealed class DisabledShortLinkClickRecorder : IShortLinkClickRecorder
{
    public Task RecordAsync(
        RecordShortLinkClickRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return Task.CompletedTask;
    }
}
