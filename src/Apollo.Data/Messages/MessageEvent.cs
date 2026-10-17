using Apollo.Domain.Messages;

namespace Apollo.Data.Messages;

/// <summary>
/// One message created, edited or deleted by a tracked user (scope §5.1): metadata only, never content. Append-only.
/// The content columns are <see langword="null"/> on <see cref="MessageEventKind.Delete"/> rows, since a delete carries
/// no content.
/// </summary>
public sealed class MessageEvent
{
    public long Id { get; init; }

    public Guid GuildId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public Guid UserId { get; init; }

    /// <summary>
    /// The channel or thread the message is in.
    /// </summary>
    public Guid ChannelId { get; init; }

    /// <summary>
    /// For a message in a thread, the thread's parent channel (scope §5.1, D2).
    /// </summary>
    public Guid? ThreadParentId { get; init; }

    /// <summary>
    /// The platform's message ID. Together with <see cref="GuildId"/> it finds the other rows of the same message.
    /// </summary>
    public required string MessageExtId { get; init; }

    public MessageEventKind Kind { get; init; }

    public int? Length { get; init; }

    public int? WordCount { get; init; }

    public int? AttachmentCount { get; init; }

    public int? EmbedCount { get; init; }

    public int? EmojiCount { get; init; }

    public int? UserMentionCount { get; init; }

    public int? RoleMentionCount { get; init; }

    public bool? MentionsEveryone { get; init; }

    public MessageContentParts? ContentParts { get; init; }

    /// <summary>
    /// On the <see cref="MessageEventKind.Create"/> row of a reply, the replied-to author if tracked; otherwise
    /// <see langword="null"/> (scope §5.1.1).
    /// </summary>
    public Guid? ReplyToUserId { get; init; }

    public bool IsBot { get; init; }
}
