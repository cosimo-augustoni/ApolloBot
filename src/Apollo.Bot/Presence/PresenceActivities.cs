using Apollo.Domain.Presence;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Bot.Presence;

/// <summary>
/// The activities of a presence that get a session (#19), each with its rich presence (#115): custom statuses are
/// user-typed text, not activities, and are ignored; a user who is offline has none, whatever the platform reports.
/// Every text is cut to <see cref="PresenceActivityDetails.MaxTextLength"/>.
/// </summary>
internal static class PresenceActivities
{
    public static IReadOnlyList<ObservedActivity> From(PlatformPresence presence)
    {
        ArgumentNullException.ThrowIfNull(presence);
        if (presence.Status == PlatformPresenceStatus.Offline)
        {
            return [];
        }

        return [.. presence.Activities
            .Select(activity => ToType(activity.Type) is { } type
                ? new ObservedActivity(new PresenceActivity(type, activity.Name), ToDetails(activity))
                : null)
            .OfType<ObservedActivity>()];
    }

    private static PresenceActivityDetails ToDetails(PlatformActivity activity) => new(
        Details: PresenceActivityDetails.Cut(activity.Details),
        State: PresenceActivityDetails.Cut(activity.State),
        LargeText: PresenceActivityDetails.Cut(activity.LargeText),
        SmallText: PresenceActivityDetails.Cut(activity.SmallText),
        PartyId: PresenceActivityDetails.Cut(activity.PartyId),
        PartySize: activity.PartySize,
        PartyMax: activity.PartyMax,
        ApplicationId: PresenceActivityDetails.Cut(activity.ApplicationId),
        ReportedStart: activity.ReportedStart,
        ReportedEnd: activity.ReportedEnd);

    private static PresenceActivityType? ToType(PlatformActivityType type) => type switch
    {
        PlatformActivityType.Playing => PresenceActivityType.Playing,
        PlatformActivityType.Streaming => PresenceActivityType.Streaming,
        PlatformActivityType.Listening => PresenceActivityType.Listening,
        PlatformActivityType.Watching => PresenceActivityType.Watching,
        PlatformActivityType.Competing => PresenceActivityType.Competing,
        _ => null,
    };
}
