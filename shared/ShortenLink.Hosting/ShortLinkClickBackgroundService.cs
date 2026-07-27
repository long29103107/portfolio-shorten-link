using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;

namespace ShortenLink.Hosting;

internal sealed class ShortLinkClickBackgroundService : BackgroundService
{
    private readonly ChannelReader<RecordShortLinkClickRequest> reader;
    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<ShortLinkClickBackgroundService> logger;

    public ShortLinkClickBackgroundService(
        Channel<RecordShortLinkClickRequest> channel,
        IServiceScopeFactory scopeFactory,
        ILogger<ShortLinkClickBackgroundService> logger)
    {
        ArgumentNullException.ThrowIfNull(channel);

        reader = channel.Reader;
        this.scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var request in reader.ReadAllAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IShortLinkClickRepository>();
                var shortLinkClick = new ShortLinkClickEntity(
                    request.ShortCode,
                    request.ClickedAtUtc,
                    request.RemoteIpAddress,
                    request.UserAgent,
                    request.Referrer);

                await repository.AddAsync(shortLinkClick, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Failed to persist short-link click analytics event for code {ShortCode}.",
                    request.ShortCode);
            }
        }
    }
}
