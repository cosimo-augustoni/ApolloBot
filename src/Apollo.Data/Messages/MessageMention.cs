namespace Apollo.Data.Messages;

/// <summary>
/// One distinct user a message mentions, written on create only: the mention graph (scope §5.1). Append-only.
/// </summary>
public sealed class MessageMention
{
    public long Id { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public Guid GuildId { get; init; }

    public Guid ChannelId { get; init; }

    public required string MessageExtId { get; init; }

    /// <summary>
    /// The author of the message.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// The mentioned user if tracked; otherwise <see langword="null"/> (scope §5.1.1).
    /// </summary>
    public Guid? MentionedUserId { get; init; }
}
