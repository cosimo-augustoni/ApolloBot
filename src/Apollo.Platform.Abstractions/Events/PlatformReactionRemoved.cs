using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A reaction was removed from a message.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Channel">The channel or thread.</param>
/// <param name="Message">The message reacted to.</param>
/// <param name="User">The user who reacted.</param>
/// <param name="MessageAuthor">The message author when the payload has it (Discord on add only); otherwise the recorder looks it up.</param>
/// <param name="Emoji">The emoji.</param>
public sealed record PlatformReactionRemoved(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformRef Channel,
    PlatformRef Message,
    PlatformRef User,
    PlatformRef? MessageAuthor,
    PlatformEmoji Emoji)
    : PlatformEvent(Guild, OccurredAt);
