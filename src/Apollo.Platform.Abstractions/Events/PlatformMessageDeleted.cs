namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A message was deleted.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Channel">The channel or thread.</param>
/// <param name="Message">The message.</param>
/// <param name="Author">The author: Fluxer sends it, Discord does not. When missing, the recorder takes it from its own create row.</param>
public sealed record PlatformMessageDeleted(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformRef Channel,
    PlatformRef Message,
    PlatformRef? Author)
    : PlatformEvent(Guild, OccurredAt);
