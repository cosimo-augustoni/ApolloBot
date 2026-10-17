using Apollo.Bot.Tracking;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Bot.Reactions;

public static class ReactionRecordingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the reaction recorder (#17) for reaction add and remove events. Needs
    /// <see cref="TrackingServiceCollectionExtensions.AddTrackingGate"/>, the platform event dispatcher and the
    /// <c>ApolloDbContext</c>.
    /// </summary>
    public static IServiceCollection AddReactionRecording(this IServiceCollection services)
    {
        services.AddPlatformEventHandler<PlatformReactionAdded, ReactionRecorder>();
        services.AddPlatformEventHandler<PlatformReactionRemoved, ReactionRecorder>();

        return services;
    }
}
