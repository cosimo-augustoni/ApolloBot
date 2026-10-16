namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A channel, category or thread was deleted.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Channel">The channel.</param>
public sealed record PlatformChannelDeleted(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformRef Channel)
    : PlatformEvent(Guild, OccurredAt);
