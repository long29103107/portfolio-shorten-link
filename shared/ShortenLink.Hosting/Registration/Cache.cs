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

        services.TryAddSingleton<IShortLinkCache, DistributedCache>();
    }
}
