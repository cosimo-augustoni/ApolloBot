using System.Globalization;
using Apollo.Data.Reactions;

namespace Apollo.Testing.Data;

/// <summary>
/// Builds a <see cref="ReactionEvent"/> row: by default an <see cref="ReactionEventKind.Add"/> of 👍 at
/// <see cref="TestTime.Now"/> on a message without a known author. Guild, users and channel are internal IDs; seed them
/// first.
/// </summary>
public sealed class ReactionEventBuilder : TestDataBuilder<ReactionEventBuilder, ReactionEvent>
{
    public ReactionEventBuilder In(Guid guildId, Guid channelId) => With(e => Copy(e, guildId: guildId, channelId: channelId));

    public ReactionEventBuilder By(Guid userId) => With(e => Copy(e, userId: userId));

    /// <summary>
    /// The reacted-to message's author, as the recorder stores it (<see langword="null"/> when unknown or untracked).
    /// </summary>
    public ReactionEventBuilder OnMessageBy(Guid? authorId) => With(e => Copy(e, messageAuthorId: authorId, clearAuthor: authorId is null));

    public ReactionEventBuilder At(DateTimeOffset occurredAt) => With(e => Copy(e, occurredAt: occurredAt));

    public ReactionEventBuilder At(string occurredAtUtc) => At(DateTimeOffset.Parse(occurredAtUtc, CultureInfo.InvariantCulture));

    public ReactionEventBuilder Removed() => With(e => Copy(e, kind: ReactionEventKind.Remove));

    /// <summary>
    /// A unicode emoji, stored as its name without a custom ID.
    /// </summary>
    public ReactionEventBuilder WithEmoji(string emoji) => With(e => Copy(e, emojiName: emoji, clearCustomId: true));

    /// <summary>
    /// A custom emoji, stored as its platform ID and name.
    /// </summary>
    public ReactionEventBuilder WithCustomEmoji(string customId, string name) => With(e => Copy(e, emojiName: name, emojiCustomId: customId));

    protected override ReactionEvent CreateDefault() => new()
    {
        OccurredAt = TestTime.Now,
        MessageExtId = Guid.NewGuid().ToString("N"),
        EmojiName = "👍",
        Kind = ReactionEventKind.Add,
    };

    private static ReactionEvent Copy(
        ReactionEvent e,
        Guid? guildId = null,
        Guid? userId = null,
        Guid? channelId = null,
        Guid? messageAuthorId = null,
        bool clearAuthor = false,
        DateTimeOffset? occurredAt = null,
        ReactionEventKind? kind = null,
        string? emojiName = null,
        string? emojiCustomId = null,
        bool clearCustomId = false) => new()
        {
            GuildId = guildId ?? e.GuildId,
            UserId = userId ?? e.UserId,
            ChannelId = channelId ?? e.ChannelId,
            MessageExtId = e.MessageExtId,
            MessageAuthorId = clearAuthor ? null : messageAuthorId ?? e.MessageAuthorId,
            EmojiName = emojiName ?? e.EmojiName,
            EmojiCustomId = clearCustomId ? null : emojiCustomId ?? e.EmojiCustomId,
            OccurredAt = occurredAt ?? e.OccurredAt,
            Kind = kind ?? e.Kind,
        };
}
