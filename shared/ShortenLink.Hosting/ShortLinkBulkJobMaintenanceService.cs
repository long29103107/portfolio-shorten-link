using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShortenLink.Application.Features.ShortLinks.Bulk;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Hosting;

internal sealed class ShortLinkBulkJobMaintenanceService(
    IServiceScopeFactory scopeFactory,
    IOptions<ShortenLinkOptions> options,
    ILogger<ShortLinkBulkJobMaintenanceService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(options.Value.BulkJobs.MaintenanceIntervalSeconds));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await CleanupAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Bulk job retention cleanup failed.");
            }
        }
    }

    private async Task CleanupAsync(CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-options.Value.BulkJobs.RetentionMinutes);
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ShortLinkDbContext>();
        var removed = await db.ShortLinkBulkJobs
            .Where(job => job.CompletedAtUtc < cutoff
                && job.Status != ShortLinkBulkJobStatuses.Queued
                && job.Status != ShortLinkBulkJobStatuses.Running)
            .ExecuteDeleteAsync(cancellationToken);
        if (removed > 0)
            logger.LogInformation("Removed {RemovedCount} expired bulk jobs.", removed);
    }
}
