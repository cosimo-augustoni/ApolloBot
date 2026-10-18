using System.Globalization;
using Apollo.Data.Messages;

namespace Apollo.Testing.Data;

/// <summary>
/// Builds a <see cref="MessageEvent"/> row: by default a <see cref="MessageEventKind.Create"/> of 10 characters at
/// <see cref="TestTime.Now"/>, not from a bot. Guild, user and channel are internal IDs; seed them first.
/// </summary>
public sealed class MessageEventBuilder : TestDataBuilder<MessageEventBuilder, MessageEvent>
{
    public MessageEventBuilder In(Guid guildId, Guid channelId, Guid? threadParentId = null) =>
        With(e => Copy(e, guildId: guildId, channelId: channelId, threadParentId: threadParentId));

    public MessageEventBuilder By(Guid userId) => With(e => Copy(e, userId: userId));

    public MessageEventBuilder At(DateTimeOffset occurredAt) => With(e => Copy(e, occurredAt: occurredAt));

    public MessageEventBuilder At(string occurredAtUtc) => At(DateTimeOffset.Parse(occurredAtUtc, CultureInfo.InvariantCulture));

    /// <summary>
    /// An edit or delete row. A delete row has no content metadata, as the recorder stores it.
    /// </summary>
    public MessageEventBuilder OfKind(MessageEventKind kind) => With(e => Copy(e, kind: kind));

    public MessageEventBuilder WithLength(int length) => With(e => Copy(e, length: length));

    public MessageEventBuilder FromBot() => With(e => Copy(e, isBot: true));

    protected override MessageEvent CreateDefault() => new()
    {
        OccurredAt = TestTime.Now,
        MessageExtId = Guid.NewGuid().ToString("N"),
        Kind = MessageEventKind.Create,
        Length = 10,
        WordCount = 2,
        AttachmentCount = 0,
        EmbedCount = 0,
        EmojiCount = 0,
        UserMentionCount = 0,
        RoleMentionCount = 0,
        MentionsEveryone = false,
    };

    private static MessageEvent Copy(
        MessageEvent e,
        Guid? guildId = null,
        Guid? userId = null,
        Guid? channelId = null,
        Guid? threadParentId = null,
        DateTimeOffset? occurredAt = null,
        MessageEventKind? kind = null,
        int? length = null,
        bool? isBot = null) => new()
        {
            GuildId = guildId ?? e.GuildId,
            UserId = userId ?? e.UserId,
            ChannelId = channelId ?? e.ChannelId,
            ThreadParentId = threadParentId ?? e.ThreadParentId,
            OccurredAt = occurredAt ?? e.OccurredAt,
            MessageExtId = e.MessageExtId,
            Kind = kind ?? e.Kind,
            Length = kind == MessageEventKind.Delete ? null : length ?? e.Length,
            WordCount = e.WordCount,
            AttachmentCount = e.AttachmentCount,
            EmbedCount = e.EmbedCount,
            EmojiCount = e.EmojiCount,
            UserMentionCount = e.UserMentionCount,
            RoleMentionCount = e.RoleMentionCount,
            MentionsEveryone = e.MentionsEveryone,
            ContentParts = e.ContentParts,
            ReplyToUserId = e.ReplyToUserId,
            IsBot = isBot ?? e.IsBot,
        };
}
