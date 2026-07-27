using System.Reflection;
using ShortenLink.Application.Behaviors;
using ShortenLink.Mediator;

namespace ShortenLink.Api;

internal static class MediatorServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationMediator(
        this IServiceCollection services,
        params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(assemblies);

        foreach (var implementationType in assemblies
                     .Distinct()
                     .SelectMany(static assembly => assembly.DefinedTypes)
                     .Where(static type => type is { IsAbstract: false, IsInterface: false }))
        {
            foreach (var serviceType in implementationType.ImplementedInterfaces.Where(IsMediatorHandler))
            {
                services.AddScoped(serviceType, implementationType);
            }
        }

        services.AddScoped<MediatorServiceFactory>(serviceProvider =>
            serviceType => serviceProvider.GetRequiredService(serviceType));
        services.AddScoped<MediatorServiceEnumerableFactory>(serviceProvider =>
            serviceType => serviceProvider.GetServices(serviceType).Cast<object>());
        services.AddScoped<ApplicationMediator>();
        services.AddScoped<ISender>(serviceProvider => serviceProvider.GetRequiredService<ApplicationMediator>());
        services.AddScoped<IPublisher>(serviceProvider => serviceProvider.GetRequiredService<ApplicationMediator>());
        services.AddScoped<IMediator>(serviceProvider => serviceProvider.GetRequiredService<ApplicationMediator>());
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(LoggingPipelineBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>));
        services.AddScoped(typeof(IPipelineBehavior<,>), typeof(UnitOfWorkPipelineBehavior<,>));

        return services;
    }

    private static bool IsMediatorHandler(Type type) =>
        !type.ContainsGenericParameters
        && type.IsGenericType
        && type.GetGenericTypeDefinition() is var definition
        && (definition == typeof(IRequestHandler<,>)
            || definition == typeof(INotificationHandler<>)
            || definition == typeof(IPipelineBehavior<,>));
}
