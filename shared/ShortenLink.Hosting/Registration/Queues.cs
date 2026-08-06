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
    private static void RegisterAnalytics(IServiceCollection services)
    {
        services.TryAddSingleton<IMessageQueue<RecordShortLinkClickRequest>>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;
            var capacity = options.Queue.AnalyticsCapacity > 0
                ? options.Queue.AnalyticsCapacity
                : options.Analytics.QueueCapacity;
            return MessageQueueFactory.Create<RecordShortLinkClickRequest>(
                new MessageQueueOptions
                {
                    Provider = options.Queue.Provider,
                    RabbitMqConnectionString = options.Queue.RabbitMqConnectionString,
                    Capacity = capacity,
                    PrefetchCount = options.Queue.PrefetchCount
                },
                options.Queue.AnalyticsQueueName);
        });

        services.AddSingleton<IHostedService>(serviceProvider =>
        {
            var analyticsOptions = serviceProvider
                .GetRequiredService<IOptions<ShortenLinkOptions>>()
                .Value
                .Analytics;
            return analyticsOptions.Enabled && analyticsOptions.UseAsyncWorker
                ? ActivatorUtilities.CreateInstance<ClickWorker>(serviceProvider)
                : new DisabledAnalyticsHostedService();
        });

        services.TryAddScoped<IShortLinkClickRecorder>(serviceProvider =>
        {
            var currentAnalyticsOptions = serviceProvider
                .GetRequiredService<IOptions<ShortenLinkOptions>>()
                .Value
                .Analytics;

            if (!currentAnalyticsOptions.Enabled)
            {
                return new DisabledClickRecorder();
            }

            if (!currentAnalyticsOptions.UseAsyncWorker)
            {
                return new SyncClickRecorder(
                    serviceProvider.GetRequiredService<IShortLinkClickRepository>());
            }

            return new ClickRecorder(
                serviceProvider.GetRequiredService<IMessageQueue<RecordShortLinkClickRequest>>(),
                serviceProvider.GetRequiredService<ILogger<ClickRecorder>>());
        });
    }

    private static void RegisterAuditQueue(IServiceCollection services)
    {
        services.TryAddSingleton<IMessageQueue<AuditEvent>>(serviceProvider =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<ShortenLinkOptions>>().Value;
            return MessageQueueFactory.Create<AuditEvent>(
                new MessageQueueOptions
                {
                    Provider = options.Queue.Provider,
                    RabbitMqConnectionString = options.Queue.RabbitMqConnectionString,
                    Capacity = options.Queue.AuditCapacity,
                    PrefetchCount = options.Queue.PrefetchCount
                },
                options.Queue.AuditQueueName);
        });
        services.TryAddSingleton<IAuditEventQueue, AuditQueue>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, AuditWorker>());
    }

    private sealed class DisabledAnalyticsHostedService : IHostedService
    {
        public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
