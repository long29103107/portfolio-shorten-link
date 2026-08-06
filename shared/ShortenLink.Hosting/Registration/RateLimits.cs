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
    private static void RegisterRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = static (context, _) =>
            {
                var policy = context.HttpContext
                    .GetEndpoint()?
                    .Metadata
                    .GetMetadata<EnableRateLimitingAttribute>()?
                    .PolicyName;
                context.HttpContext.RequestServices
                    .GetRequiredService<RateMonitor>()
                    .RecordRejection(policy);
                return ValueTask.CompletedTask;
            };
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
}
