using System.Threading.Channels;
using System.Threading.RateLimiting;
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
using ShortenLink.Core.Services;
using ShortenLink.Infrastructure.Persistence;
using ShortenLink.Infrastructure.Repositories;

namespace ShortenLink.AspNetCore;

public static class ShortenLinkServiceCollectionExtensions
{
    public static IServiceCollection AddShortenLink(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services
            .AddOptions<ShortenLinkOptions>()
            .Bind(configuration.GetSection(ShortenLinkOptions.SectionName))
            .Validate(
                static options => HasRequiredConnectionString(options.Database),
                "ShortenLink database configuration requires SqliteConnectionString when UsePostgres is false, or PostgresConnectionString when UsePostgres is true.")
            .Validate(
                static options => string.IsNullOrWhiteSpace(options.BaseUrl)
                    || Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
                        && (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps),
                "ShortenLink:BaseUrl must be an absolute HTTP or HTTPS URL when provided.")
            .Validate(
                static options => IsValidFrontendFallbackPath(options.Redirect.FrontendFallbackPath),
                "ShortenLink:Redirect:FrontendFallbackPath must be a root-relative path or an absolute HTTP/HTTPS URL.")
            .Validate(
                static options => options.Analytics.QueueCapacity > 0,
                "ShortenLink:Analytics:QueueCapacity must be greater than 0.")
            .Validate(
                static options => IsValidCacheProvider(options.Cache),
                "ShortenLink:Cache:Provider must be Memory or Redis.")
            .Validate(
                static options => options.Cache.EntryTtlSeconds > 0,
                "ShortenLink:Cache:EntryTtlSeconds must be greater than 0.")
            .Validate(
                static options => !IsRedisCacheEnabled(options.Cache)
                    || !string.IsNullOrWhiteSpace(options.Cache.RedisConnectionString),
                "ShortenLink:Cache:RedisConnectionString is required when Redis cache is enabled.")
            .Validate(
                static options => HasValidRateLimit(options.RateLimiting.Create)
                    && HasValidRateLimit(options.RateLimiting.Redirect),
                "ShortenLink:RateLimiting create and redirect policies require PermitLimit > 0, WindowSeconds > 0, and QueueLimit >= 0.")
            .Validate(
                static options => !options.Security.Enabled || HasValidSecurityOptions(options.Security),
                "ShortenLink:Security requires HeaderName and at least one API key when enabled.")
            .Validate(
                static options => options.Security.SessionTokenTtlMinutes > 0,
                "ShortenLink:Security:SessionTokenTtlMinutes must be greater than 0.")
            .Validate(
                static options => options.Security.RefreshTokenTtlMinutes > 0,
                "ShortenLink:Security:RefreshTokenTtlMinutes must be greater than 0.")
            .ValidateOnStart();

        services.AddDbContext<ShortLinkDbContext>((serviceProvider, options) =>
        {
            var shortenLinkOptions = serviceProvider
                .GetRequiredService<IOptions<ShortenLinkOptions>>()
                .Value;

            if (shortenLinkOptions.Database.UsePostgres)
            {
                options.UseNpgsql(shortenLinkOptions.Database.PostgresConnectionString);
                return;
            }

            options.UseSqlite(shortenLinkOptions.Database.SqliteConnectionString);
        });

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.TryAddSingleton<IShortCodeGenerator, Base62ShortCodeGenerator>();
        services.TryAddScoped<IShortLinkRepository, EfCoreShortLinkRepository>();
        services.TryAddScoped<IShortLinkClickRepository, EfCoreShortLinkClickRepository>();
        services.TryAddScoped<IShortLinkShareRepository, EfCoreShortLinkShareRepository>();
        services.TryAddScoped<IShortLinkAuditRepository, EfCoreShortLinkAuditRepository>();
        services.TryAddScoped<IShortenLinkSecurityAssignmentRepository, EfCoreShortenLinkSecurityAssignmentRepository>();
        services.TryAddScoped<IShortenLinkSecurityRoleRepository, EfCoreShortenLinkSecurityRoleRepository>();
        services.TryAddScoped<IShortenLinkSecurityUserRepository, EfCoreShortenLinkSecurityUserRepository>();
        services.TryAddScoped<IShortenLinkUserApiKeyRepository, EfCoreShortenLinkUserApiKeyRepository>();
        services.TryAddScoped<IShortLinkService, ShortLinkService>();
        services.TryAddScoped<IShortenLinkUserSessionService, ShortenLinkUserSessionService>();
        services.TryAddScoped<IShortenLinkAuthorizationService, ShortenLinkAuthorizationService>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, ShortLinkDatabaseInitializationService>());

        RegisterCache(services, configuration);
        RegisterRateLimiting(services);
        RegisterAnalytics(services);

        return services;
    }

    private static void RegisterRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(
                ShortenLinkRateLimitingPolicyNames.Create,
                httpContext => CreateFixedWindowPartition(
                    httpContext,
                    httpContext.RequestServices
                        .GetRequiredService<IOptions<ShortenLinkOptions>>()
                        .Value
                        .RateLimiting
                        .Create));
            options.AddPolicy(
                ShortenLinkRateLimitingPolicyNames.Redirect,
                httpContext => CreateFixedWindowPartition(
                    httpContext,
                    httpContext.RequestServices
                        .GetRequiredService<IOptions<ShortenLinkOptions>>()
                        .Value
                        .RateLimiting
                        .Redirect));
        });
    }

    private static void RegisterCache(IServiceCollection services, IConfiguration configuration)
    {
        var cacheOptions = configuration
            .GetSection(ShortenLinkOptions.SectionName)
            .Get<ShortenLinkOptions>()
            ?.Cache ?? new ShortenLinkCacheOptions();

        if (!cacheOptions.Enabled)
        {
            services.TryAddSingleton<IShortLinkCache, DisabledShortLinkCache>();
            return;
        }

        if (IsRedisCacheEnabled(cacheOptions))
        {
            services.AddStackExchangeRedisCache(options =>
            {
                options.Configuration = cacheOptions.RedisConnectionString;
                options.InstanceName = "ShortenLink:";
            });
        }
        else
        {
            services.AddDistributedMemoryCache();
        }

        services.TryAddSingleton<IShortLinkCache, DistributedShortLinkCache>();
    }

    private static void RegisterAnalytics(IServiceCollection services)
    {
        services.TryAddSingleton(serviceProvider =>
        {
            var analyticsOptions = serviceProvider
                .GetRequiredService<IOptions<ShortenLinkOptions>>()
                .Value
                .Analytics;

            return Channel.CreateBounded<RecordShortLinkClickRequest>(new BoundedChannelOptions(analyticsOptions.QueueCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropWrite
            });
        });
        services.TryAddScoped<IShortLinkClickRecorder>(serviceProvider =>
        {
            var analyticsOptions = serviceProvider
                .GetRequiredService<IOptions<ShortenLinkOptions>>()
                .Value
                .Analytics;

            if (!analyticsOptions.Enabled)
            {
                return new DisabledShortLinkClickRecorder();
            }

            if (!analyticsOptions.UseAsyncWorker)
            {
                return new SynchronousShortLinkClickRecorder(
                    serviceProvider.GetRequiredService<IShortLinkClickRepository>());
            }

            return new ChannelShortLinkClickRecorder(
                serviceProvider.GetRequiredService<Channel<RecordShortLinkClickRequest>>(),
                serviceProvider.GetRequiredService<ILogger<ChannelShortLinkClickRecorder>>());
        });
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, ShortLinkClickBackgroundService>());
    }

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
