using Apollo.Platform.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apollo.Platform.Abstractions.Dispatch;

public static class PlatformEventServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPlatformEventDispatcher"/>. Safe to call more than once.
    /// </summary>
    public static IServiceCollection AddPlatformEventDispatcher(this IServiceCollection services)
    {
        services.TryAddSingleton<IPlatformEventDispatcher, PlatformEventDispatcher>();
        return services;
    }

    /// <summary>
    /// Registers a scoped handler for <typeparamref name="TEvent"/>. Handlers run in registration order.
    /// </summary>
    public static IServiceCollection AddPlatformEventHandler<TEvent, THandler>(this IServiceCollection services)
        where TEvent : PlatformEvent
        where THandler : class, IPlatformEventHandler<TEvent>
    {
        services.AddScoped<IPlatformEventHandler<TEvent>, THandler>();
        return services;
    }
}
