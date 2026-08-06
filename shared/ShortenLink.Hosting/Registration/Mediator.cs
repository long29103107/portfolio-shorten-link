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
    private static void RegisterApplicationMediator(
        IServiceCollection services,
        params System.Reflection.Assembly[] assemblies)
    {
        foreach (var implementationType in assemblies
                     .Distinct()
                     .SelectMany(static assembly => assembly.DefinedTypes)
                     .Where(static type => type is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var serviceType in implementationType.ImplementedInterfaces.Where(IsMediatorHandler))
            {
                services.TryAddScoped(serviceType, implementationType);
            }
        }

        services.TryAddScoped<MediatorServiceFactory>(serviceProvider =>
            serviceType => serviceProvider.GetRequiredService(serviceType));
        services.TryAddScoped<MediatorServiceEnumerableFactory>(serviceProvider =>
            serviceType => serviceProvider.GetServices(serviceType).Cast<object>());
        services.TryAddScoped<ApplicationMediator>();
        services.TryAddScoped<ISender>(serviceProvider => serviceProvider.GetRequiredService<ApplicationMediator>());
        services.TryAddScoped<IPublisher>(serviceProvider => serviceProvider.GetRequiredService<ApplicationMediator>());
        services.TryAddScoped<IMediator>(serviceProvider => serviceProvider.GetRequiredService<ApplicationMediator>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped(
            typeof(IPipelineBehavior<,>),
            typeof(LoggingPipelineBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationPipelineBehavior<,>)));
        services.TryAddEnumerable(ServiceDescriptor.Scoped(
            typeof(IPipelineBehavior<,>),
            typeof(UnitOfWorkPipelineBehavior<,>)));
    }

    private static bool IsMediatorHandler(Type type) =>
        !type.ContainsGenericParameters
        && type.IsGenericType
        && type.GetGenericTypeDefinition() is var definition
        && (definition == typeof(IRequestHandler<,>)
            || definition == typeof(INotificationHandler<>));
}
