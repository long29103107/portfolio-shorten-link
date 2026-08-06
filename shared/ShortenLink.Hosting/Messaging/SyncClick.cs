using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;

namespace ShortenLink.Hosting;

internal sealed class SyncClickRecorder : IShortLinkClickRecorder
{
    private readonly IShortLinkClickRepository repository;

    public SyncClickRecorder(IShortLinkClickRepository repository)
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
            request.Referrer,
            tenantId: request.TenantId);

        return repository.AddAsync(shortLinkClick, cancellationToken);
    }
}
