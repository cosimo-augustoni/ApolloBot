using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A channel, category or thread was created.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Channel">The new channel.</param>
public sealed record PlatformChannelCreated(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformChannel Channel)
    : PlatformEvent(Guild, OccurredAt);
