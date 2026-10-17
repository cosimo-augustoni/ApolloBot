using Apollo.Domain.Presence;
using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Bot.Presence;

/// <summary>
/// The activities of a presence that get a session (#19): custom statuses are user-typed text, not activities, and are
/// ignored; a user who is offline has none, whatever the platform reports.
/// </summary>
internal static class PresenceActivities
{
    public static IReadOnlyList<PresenceActivity> From(PlatformPresence presence)
    {
        ArgumentNullException.ThrowIfNull(presence);
        if (presence.Status == PlatformPresenceStatus.Offline)
        {
            return [];
        }

        return [.. presence.Activities
            .Select(activity => ToType(activity.Type) is { } type ? new PresenceActivity(type, activity.Name) : null)
            .OfType<PresenceActivity>()];
    }

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
