namespace Apollo.Data.Reactions;

/// <summary>
/// One reaction added or removed by a tracked user (scope §5.1). Append-only.
/// </summary>
public sealed class ReactionEvent
{
    public long Id { get; init; }

    public Guid GuildId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// The user who reacted.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// The channel or thread the message is in.
    /// </summary>
    public Guid ChannelId { get; init; }

    /// <summary>
    /// The platform's message ID (messages have no internal ID). Together with <see cref="GuildId"/> it finds the
    /// message's other reaction rows and its <c>message_events</c> rows.
    /// </summary>
    public required string MessageExtId { get; init; }

    /// <summary>
    /// The message author if tracked; otherwise <see langword="null"/> (scope §5.1.1), also when unknown.
    /// </summary>
    public Guid? MessageAuthorId { get; init; }

    /// <summary>
    /// The emoji itself for a unicode emoji, the custom emoji's name otherwise (empty when the platform no longer
    /// knows it).
    /// </summary>
    public required string EmojiName { get; init; }

    /// <summary>
    /// The platform's custom emoji ID, or <see langword="null"/> for a unicode emoji.
    /// </summary>
    public string? EmojiCustomId { get; init; }

    public ReactionEventKind Kind { get; init; }
}
