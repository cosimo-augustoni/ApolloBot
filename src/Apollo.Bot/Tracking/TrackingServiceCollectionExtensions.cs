using Apollo.Bot.GuildState;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apollo.Bot.Tracking;

public static class TrackingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the tracking gate (#14): <see cref="ITrackingGate"/>, its handlers for the guild state notifications and
    /// the 30 s <see cref="TrackingGateReloader"/>. Needs <see cref="GuildStateServiceCollectionExtensions.AddGuildStateSync"/>,
    /// an <see cref="Apollo.Platform.Abstractions.IChatPlatform"/> and the <c>ApolloDbContext</c>.
    /// </summary>
    public static IServiceCollection AddTrackingGate(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<TrackingStateLoader>();
        services.AddSingleton<TrackingGate>();
        services.AddSingleton<ITrackingGate>(serviceProvider => serviceProvider.GetRequiredService<TrackingGate>());
        services.AddGuildStateNotificationHandler<GuildStateSynchronized, TrackingGateNotificationHandler>();
        services.AddGuildStateNotificationHandler<MemberAccessStateChanged, TrackingGateNotificationHandler>();
        services.AddHostedService<TrackingGateReloader>();

        return services;
    }

    /// <summary>
    /// Registers a scoped <see cref="ITrackingLifecycleHandler"/>. Handlers run in registration order.
    /// </summary>
    public static IServiceCollection AddTrackingLifecycleHandler<THandler>(this IServiceCollection services)
        where THandler : class, ITrackingLifecycleHandler
    {
        services.AddScoped<ITrackingLifecycleHandler, THandler>();
        return services;
    }
}
