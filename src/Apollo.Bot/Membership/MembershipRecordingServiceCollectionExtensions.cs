using Apollo.Bot.GuildState;
using Apollo.Bot.Tracking;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Bot.Membership;

public static class MembershipRecordingServiceCollectionExtensions
{
    /// <summary>
    /// Registers the membership recorder (#18) for join, leave, ban and unban events. Register it after
    /// <see cref="GuildStateServiceCollectionExtensions.AddGuildStateSync"/>: a join is recorded only if the user is
    /// tracked once the sync has stored the new member. Needs
    /// <see cref="TrackingServiceCollectionExtensions.AddTrackingGate"/>, the platform event dispatcher and the
    /// <c>ApolloDbContext</c>.
    /// </summary>
    public static IServiceCollection AddMembershipRecording(this IServiceCollection services)
    {
        services.AddPlatformEventHandler<PlatformMemberJoined, MembershipRecorder>();
        services.AddPlatformEventHandler<PlatformMemberLeft, MembershipRecorder>();
        services.AddPlatformEventHandler<PlatformMemberBanned, MembershipRecorder>();
        services.AddPlatformEventHandler<PlatformMemberUnbanned, MembershipRecorder>();

        return services;
    }
}
