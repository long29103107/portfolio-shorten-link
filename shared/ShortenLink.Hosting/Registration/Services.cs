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
    public static IServiceCollection AddShortenLink(
        this IServiceCollection services,
        IConfiguration configuration)
        => AddShortenLink(services, configuration, configure: null);

    public static IServiceCollection AddShortenLink(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<ShortenLinkHostOptions>? configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var hostOptions = new ShortenLinkHostOptions();
        configure?.Invoke(hostOptions);

        services
            .AddOptions<ShortenLinkOptions>()
            .Bind(configuration.GetSection(ShortenLinkOptions.SectionName))
            .Validate(
                options => hostOptions.UseExternalPersistence || HasRequiredConnectionString(options.Database),
                "ShortenLink database configuration requires SqliteConnectionString when UsePostgres is false, or PostgresConnectionString when UsePostgres is true.")
            .Validate(
                static options => string.IsNullOrWhiteSpace(options.BaseUrl)
                    || Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var baseUri)
                        && (baseUri.Scheme == Uri.UriSchemeHttp || baseUri.Scheme == Uri.UriSchemeHttps),
                "ShortenLink:BaseUrl must be an absolute HTTP or HTTPS URL when provided.")
            .Validate(
                static options => options.Code.DefaultLength > 0,
                "ShortenLink:Code:DefaultLength must be greater than 0.")
            .Validate(
                static options => options.Code.MaxRetry > 0,
                "ShortenLink:Code:MaxRetry must be greater than 0.")
            .Validate(
                static options => IsValidFrontendFallbackPath(options.Redirect.FrontendFallbackPath),
                "ShortenLink:Redirect:FrontendFallbackPath must be a root-relative path or an absolute HTTP/HTTPS URL.")
            .Validate(
                static options => options.Analytics.QueueCapacity > 0,
                "ShortenLink:Analytics:QueueCapacity must be greater than 0.")
            .Validate(
                static options => HasValidQueueOptions(options.Queue),
                "ShortenLink:Queue requires positive capacities, queue names, and a RabbitMqConnectionString when RabbitMq is selected.")
            .Validate(
                static options => IsValidCacheProvider(options.Cache),
                "ShortenLink:Cache:Provider must be Memory or Redis.")
            .Validate(
                static options => options.Cache.EntryTtlSeconds > 0,
                "ShortenLink:Cache:EntryTtlSeconds must be greater than 0.")
            .Validate(
                static options => options.Cache.NegativeEntryTtlSeconds > 0,
                "ShortenLink:Cache:NegativeEntryTtlSeconds must be greater than 0.")
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

        if (!hostOptions.UseExternalPersistence)
        {
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
        }

        services.TryAddSingleton<TimeProvider>(TimeProvider.System);
        services.AddHttpContextAccessor();
        services.TryAddScoped<ICurrentRequestContext, RequestContext>();
        services.TryAddSingleton<RateMonitor>();
        services.TryAddSingleton<IRateLimitActivityReader>(serviceProvider =>
            serviceProvider.GetRequiredService<RateMonitor>());
        services.TryAddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.TryAddSingleton<IShortCodeGenerator, Base62ShortCodeGenerator>();
        services.TryAddSingleton<IShortLinkImportValidator, ShortLinkImportValidator>();
        services.TryAddSingleton<IShortLinkExpirationEvaluator, ShortLinkExpirationEvaluator>();
        if (!hostOptions.UseExternalPersistence)
        {
            services.TryAddScoped<IShortLinkRepository, EfCoreShortLinkRepository>();
            services.TryAddScoped<IShortLinkExpirationCheckpointRepository, EfCoreShortLinkExpirationCheckpointRepository>();
            services.TryAddScoped<IUnitOfWork, EfCoreUnitOfWork>();
            services.TryAddScoped<IShortLinkClickRepository, EfCoreShortLinkClickRepository>();
            services.TryAddScoped<IShortLinkShareRepository, EfCoreShortLinkShareRepository>();
            services.TryAddScoped<IAuditRepository, EfCoreShortLinkAuditRepository>();
        }
        services.TryAddScoped<AuditEventBuffer>();
        services.TryAddScoped<AuditWriter>();
        services.TryAddScoped<ShortLinkAccessGuard>();
        services.TryAddScoped<ShortLinkAuditWriter>();
        services.AddValidatorsFromAssemblyContaining<CreateShortLinkCommand>(ServiceLifetime.Scoped);
        RegisterApplicationMediator(services, typeof(CreateShortLinkCommand).Assembly);
        if (!hostOptions.RedirectOnly && !hostOptions.UseExternalPersistence)
        {
            services.TryAddScoped<IShortenLinkSecurityAssignmentRepository, EfCoreShortenLinkSecurityAssignmentRepository>();
            services.TryAddScoped<IShortenLinkSecurityRoleRepository, EfCoreShortenLinkSecurityRoleRepository>();
            services.TryAddScoped<IShortenLinkSecurityUserRepository, EfCoreShortenLinkSecurityUserRepository>();
            services.TryAddScoped<IShortenLinkUserApiKeyRepository, EfCoreShortenLinkUserApiKeyRepository>();
            services.TryAddScoped<IShortenLinkAuthorizationService, AuthorizationService>();
            services.TryAddScoped<IShortenLinkUserSessionService, UserSessionService>();
        }
        services.TryAddScoped<IShortLinkService>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;
            return new ShortLinkService(
                serviceProvider.GetRequiredService<IShortLinkRepository>(),
                serviceProvider.GetRequiredService<IShortCodeGenerator>(),
                serviceProvider.GetRequiredService<IShortLinkCache>(),
                serviceProvider.GetRequiredService<TimeProvider>(),
                options.Code.DefaultLength,
                options.Code.MaxRetry,
                serviceProvider.GetService<IShortLinkEventSink>(),
                options.Observability.Enabled);
        });
        services.TryAddScoped<IShortLinkExpirationService>(serviceProvider =>
            new ShortLinkExpirationService(
                serviceProvider.GetRequiredService<IShortLinkRepository>(),
                serviceProvider.GetRequiredService<IShortLinkExpirationEvaluator>(),
                serviceProvider.GetService<IShortLinkExpirationEventSink>()));
        if (!hostOptions.UseExternalPersistence)
        {
            services.TryAddScoped<IShortLinkExpirationCacheInvalidationSink, ExpirationCache>();
            services.TryAddScoped<IShortLinkExpirationExecutionService, ShortLinkExpirationExecutionService>();
        }
        if (!hostOptions.UseExternalPersistence)
        {
            services.AddSingleton<IHostedService>(_ =>
                new DatabaseInit(
                    _.GetRequiredService<IServiceScopeFactory>(),
                    !hostOptions.RedirectOnly));
        }

        RegisterCache(services, configuration);
        RegisterRateLimiting(services);
        RegisterAnalytics(services);
        RegisterAuditQueue(services);

        var observability = configuration
            .GetSection(ShortenLinkOptions.SectionName)
            .Get<ShortenLinkOptions>()?
            .Observability;
        if (observability?.HealthChecksEnabled == true)
        {
            services.AddShortenLinkHealthChecks();
        }

        return services;
    }
}
