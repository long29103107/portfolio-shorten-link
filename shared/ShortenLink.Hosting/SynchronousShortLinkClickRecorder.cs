using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;

namespace ShortenLink.Hosting;

internal sealed class SynchronousShortLinkClickRecorder : IShortLinkClickRecorder
{
    private readonly IShortLinkClickRepository repository;

    public SynchronousShortLinkClickRecorder(IShortLinkClickRepository repository)
    {
        this.repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public Task RecordAsync(
        RecordShortLinkClickRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var shortLinkClick = new ShortLinkClickEntity(
            request.ShortCode,
            request.ClickedAtUtc,
            request.RemoteIpAddress,
            request.UserAgent,
            request.Referrer);

        return repository.AddAsync(shortLinkClick, cancellationToken);
    }
}
