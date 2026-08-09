using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;
using ShortenLink.Messaging;

namespace ShortenLink.Hosting;

internal sealed class ClickWorker(
    IMessageQueue<RecordShortLinkClickRequest> queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ClickWorker> logger)
    : MessageDeliveryWorker<RecordShortLinkClickRequest>(queue, scopeFactory, logger)
{
    protected override async Task PersistAsync(
        RecordShortLinkClickRequest request,
        IServiceProvider services,
        CancellationToken cancellationToken)
    {
        var repository = services.GetRequiredService<IShortLinkClickRepository>();
        var shortLinkClick = new ShortLinkClickEntity(
            request.ShortCode,
            request.ClickedAtUtc,
            request.RemoteIpAddress,
            request.UserAgent,
            request.Referrer,
            tenantId: request.TenantId,
            device: request.Device,
            browser: request.Browser,
            operatingSystem: request.OperatingSystem,
            countryCode: request.CountryCode,
            visitorKeyHash: request.VisitorKeyHash);

        await repository.AddAsync(shortLinkClick, cancellationToken);
    }

    protected override void LogFailure(
        ILogger logger,
        RecordShortLinkClickRequest request,
        Exception exception) =>
        logger.LogError(
            exception,
            "Failed to persist short-link click analytics event for code {ShortCode}.",
            request.ShortCode);
}
