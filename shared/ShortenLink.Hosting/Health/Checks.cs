using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ShortenLink.Infrastructure.Persistence;

namespace ShortenLink.Hosting;

public static class ShortenLinkHealthCheckNames
{
    public const string Configuration = "shortenlink.configuration";

    public const string Database = "shortenlink.database";

    public const string Cache = "shortenlink.cache";

    public const string Analytics = "shortenlink.analytics";
}

public static class ShortenLinkHealthChecks
{
    public static IServiceCollection AddShortenLinkHealthChecks(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (services.Any(static descriptor => descriptor.ServiceType == typeof(ShortenLinkHealthChecksRegistration)))
        {
            return services;
        }

        services.AddSingleton<ShortenLinkHealthChecksRegistration>();
        services.AddHealthChecks()
            .AddCheck<ShortenLinkConfigurationHealthCheck>(
                ShortenLinkHealthCheckNames.Configuration,
                tags: ["shortenlink", "ready"])
            .AddCheck<ShortenLinkDatabaseHealthCheck>(
                ShortenLinkHealthCheckNames.Database,
                tags: ["shortenlink", "ready"])
            .AddCheck<ShortenLinkCacheHealthCheck>(
                ShortenLinkHealthCheckNames.Cache,
                tags: ["shortenlink", "ready"])
            .AddCheck<ShortenLinkAnalyticsHealthCheck>(
                ShortenLinkHealthCheckNames.Analytics,
                tags: ["shortenlink", "ready"]);

        return services;
    }

    private sealed class ShortenLinkHealthChecksRegistration
    {
    }
}

public sealed class ShortenLinkConfigurationHealthCheck(
    IOptions<ShortenLinkOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        _ = options.Value;
        return Task.FromResult(HealthCheckResult.Healthy("ShortenLink configuration is valid."));
    }
}

public sealed class ShortenLinkDatabaseHealthCheck(
    IServiceScopeFactory scopeFactory) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(scopeFactory);

        await using var scope = scopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetService<ShortLinkDbContext>();
        if (dbContext is null)
        {
            return HealthCheckResult.Healthy("ShortenLink uses host-provided persistence.");
        }

        try
        {
            return await dbContext.Database.CanConnectAsync(cancellationToken)
                ? HealthCheckResult.Healthy("ShortenLink database is reachable.")
                : HealthCheckResult.Unhealthy("ShortenLink database is unavailable.");
        }
        catch
        {
            return HealthCheckResult.Unhealthy("ShortenLink database health check failed.");
        }
    }
}

public sealed class ShortenLinkCacheHealthCheck(
    IOptions<ShortenLinkOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var cache = options.Value.Cache;
        var description = cache.Enabled
            ? $"ShortenLink {cache.Provider.ToLowerInvariant()} cache is configured."
            : "ShortenLink cache is disabled.";
        return Task.FromResult(HealthCheckResult.Healthy(description));
    }
}

public sealed class ShortenLinkAnalyticsHealthCheck(
    IOptions<ShortenLinkOptions> options) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var analytics = options.Value.Analytics;
        var description = analytics.Enabled
            ? "ShortenLink analytics is configured."
            : "ShortenLink analytics is disabled.";
        return Task.FromResult(HealthCheckResult.Healthy(description));
    }
}
