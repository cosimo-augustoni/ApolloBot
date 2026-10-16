using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A member's presence changed.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Presence">The new presence.</param>
public sealed record PlatformPresenceUpdated(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformPresence Presence)
    : PlatformEvent(Guild, OccurredAt);
