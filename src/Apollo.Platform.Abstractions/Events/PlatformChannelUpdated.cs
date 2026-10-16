using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A channel, category or thread changed.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Channel">The channel after the change.</param>
public sealed record PlatformChannelUpdated(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformChannel Channel)
    : PlatformEvent(Guild, OccurredAt);
