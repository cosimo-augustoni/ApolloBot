using Apollo.Bot.GuildState;
using Apollo.Bot.Tracking;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apollo.Bot.Presence;

public static class PresenceRecordingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the presence recorder (#19): presence updates, the tracking hooks, the reconciliation after every full
    /// guild sync and <see cref="PresenceSessionLifecycleService"/> for the shutdown close. Needs
    /// <see cref="TrackingServiceCollectionExtensions.AddTrackingGate"/> registered first (so the gate handles a guild
    /// sync before the recorder reconciles), an <see cref="Apollo.Platform.Abstractions.IChatPlatform"/> and the
    /// <c>ApolloDbContext</c>.
    /// </summary>
    public static IServiceCollection AddPresenceRecording(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<PresenceSessionStore>();
        services.AddScoped<PresenceSessionRecorder>();
        services.AddPlatformEventHandler<PlatformPresenceUpdated, PresenceSessionRecorder>();
        services.AddTrackingLifecycleHandler<PresenceSessionRecorder>();
        services.AddGuildStateNotificationHandler<GuildStateSynchronized, PresenceSessionRecorder>();
        services.AddHostedService<PresenceSessionLifecycleService>();

        return services;
    }
}
