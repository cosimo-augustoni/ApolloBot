using Apollo.Bot.Tracking;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Bot.Messages;

public static class MessageRecordingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the message recorder (#15) for message create, edit and delete events. Needs
    /// <see cref="TrackingServiceCollectionExtensions.AddTrackingGate"/>, the platform event dispatcher and the
    /// <c>ApolloDbContext</c>.
    /// </summary>
    public static IServiceCollection AddMessageRecording(this IServiceCollection services)
    {
        services.AddPlatformEventHandler<PlatformMessageCreated, MessageRecorder>();
        services.AddPlatformEventHandler<PlatformMessageUpdated, MessageRecorder>();
        services.AddPlatformEventHandler<PlatformMessageDeleted, MessageRecorder>();

        return services;
    }
}
