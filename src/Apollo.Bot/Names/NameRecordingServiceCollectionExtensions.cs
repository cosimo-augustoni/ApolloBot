using Apollo.Bot.GuildState;
using Apollo.Bot.Tracking;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Apollo.Bot.Names;

public static class NameRecordingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the member name recorder (#114): member updates, the tracking hooks (a baseline when tracking starts)
    /// and the comparison after every full guild sync. Needs
    /// <see cref="TrackingServiceCollectionExtensions.AddTrackingGate"/> registered first (so the gate handles a guild sync
    /// before the recorder compares), an <see cref="Apollo.Platform.Abstractions.IChatPlatform"/> and the
    /// <c>ApolloDbContext</c>.
    /// </summary>
    public static IServiceCollection AddNameRecording(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<MemberNameLock>();
        services.AddPlatformEventHandler<PlatformMemberUpdated, MemberNameRecorder>();
        services.AddTrackingLifecycleHandler<MemberNameRecorder>();
        services.AddGuildStateNotificationHandler<GuildStateSynchronized, MemberNameRecorder>();

        return services;
    }
}
