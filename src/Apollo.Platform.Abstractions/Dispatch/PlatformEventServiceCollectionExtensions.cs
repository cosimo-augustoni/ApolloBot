using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apollo.Platform.Abstractions.Dispatch;

public static class PlatformEventServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="IPlatformEventDispatcher"/>, <see cref="IPlatformEventActivity"/> (the same instance) and
    /// <see cref="RecordedEventMetrics"/> with the <c>IMeterFactory</c> it needs (the host's, if it has one). Safe to call
    /// more than once.
    /// </summary>
    public static IServiceCollection AddPlatformEventDispatcher(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<PlatformEventDispatcher>();
        services.TryAddSingleton<IPlatformEventDispatcher>(sp => sp.GetRequiredService<PlatformEventDispatcher>());
        services.TryAddSingleton<IPlatformEventActivity>(sp => sp.GetRequiredService<PlatformEventDispatcher>());
        services.AddMetrics();
        services.TryAddSingleton<RecordedEventMetrics>();
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
