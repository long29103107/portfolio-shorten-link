using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShortenLink.Core.Generation;
using ShortenLink.Core.Domain;
using ShortenLink.Core.Services;
using ShortenLink.Core.Abstractions;
using ShortenLink.Application.Services;
using ShortenLink.Application.Abstractions;
using ShortenLink.Application.Behaviors;
using ShortenLink.Application.Features.Audit;
using ShortenLink.Application.Features.ShortLinks;
using ShortenLink.Application.Features.ShortLinks.Create;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Repositories;
using ShortenLink.Mediator;
using ShortenLink.Messaging;

namespace ShortenLink.Hosting;

public static partial class Services
{
    private static bool IsValidFrontendFallbackPath(string? fallbackPath)
    {
        if (string.IsNullOrWhiteSpace(fallbackPath))
        {
            return true;
        }

        if (fallbackPath.StartsWith("/", StringComparison.Ordinal))
        {
            return true;
        }

        return Uri.TryCreate(fallbackPath, UriKind.Absolute, out var absoluteFallbackUri)
            && (absoluteFallbackUri.Scheme == Uri.UriSchemeHttp
                || absoluteFallbackUri.Scheme == Uri.UriSchemeHttps);
    }

    private static bool HasValidQueueOptions(ShortenLinkQueueOptions options)
    {
        if (!Enum.IsDefined(options.Provider)
            || options.AuditCapacity <= 0
            || options.AnalyticsCapacity < 0
            || options.PrefetchCount == 0
            || string.IsNullOrWhiteSpace(options.AuditQueueName)
            || string.IsNullOrWhiteSpace(options.AnalyticsQueueName))
        {
            return false;
        }

        return options.Provider != MessageQueueProvider.RabbitMq
            || IsValidRabbitMqConnectionString(options.RabbitMqConnectionString);
    }

    private static bool IsValidRabbitMqConnectionString(string? connectionString)
    {
        return Uri.TryCreate(connectionString, UriKind.Absolute, out var uri)
            && (uri.Scheme == "amqp" || uri.Scheme == "amqps");
    }

    private static bool HasRequiredConnectionString(ShortenLinkDatabaseOptions databaseOptions)
    {
        ArgumentNullException.ThrowIfNull(databaseOptions);

        return databaseOptions.UsePostgres
            ? !string.IsNullOrWhiteSpace(databaseOptions.PostgresConnectionString)
            : !string.IsNullOrWhiteSpace(databaseOptions.SqliteConnectionString);
    }

    private static bool IsValidCacheProvider(ShortenLinkCacheOptions cacheOptions)
    {
        ArgumentNullException.ThrowIfNull(cacheOptions);

        return cacheOptions.Provider.Equals("Memory", StringComparison.OrdinalIgnoreCase)
            || cacheOptions.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRedisCacheEnabled(ShortenLinkCacheOptions cacheOptions)
    {
        ArgumentNullException.ThrowIfNull(cacheOptions);

        return cacheOptions.Enabled
            && cacheOptions.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase);
    }

    private static RateLimitPartition<string> CreateFixedWindowPartition(
        HttpContext httpContext,
        ShortenLinkFixedWindowRateLimitOptions options)
    {
        var partitionKey = httpContext.Connection.RemoteIpAddress?.ToString()
            ?? httpContext.Request.Headers.Host.ToString()
            ?? "anonymous";

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = options.PermitLimit,
                QueueLimit = options.QueueLimit,
                Window = TimeSpan.FromSeconds(options.WindowSeconds)
            });
    }

    private static bool HasValidRateLimit(ShortenLinkFixedWindowRateLimitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.PermitLimit > 0
            && options.WindowSeconds > 0
            && options.QueueLimit >= 0;
    }

    private static bool HasValidSecurityOptions(ShortenLinkSecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return !string.IsNullOrWhiteSpace(options.HeaderName)
            && options.ApiKeys.Any(static key => !string.IsNullOrWhiteSpace(key.Key));
    }
}
