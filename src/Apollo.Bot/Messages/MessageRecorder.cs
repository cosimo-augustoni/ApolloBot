using Apollo.Bot.Tracking;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Messages;
using Apollo.Domain.Messages;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Messages;

/// <summary>
/// Records message create, edit and delete events of tracked users into <c>message_events</c>, and the users a new
/// message mentions into <c>message_mentions</c> (#15, scope §5.1). The events carry metadata only; content never
/// reaches the bot.
/// </summary>
/// <remarks>
/// <para>
/// The tracking gate decides by the author. When an edit or delete has no author (Discord's delete never has one), the
/// author comes from the message's own <c>create</c> row; without one (the message predates the bot, or its author was
/// untracked) nothing is stored and the event counts as dropped for <see cref="EventDropReason.Untracked"/>.
/// </para>
/// <para>
/// References to other users (reply target, mentioned users) are stored only for tracked users, else <c>NULL</c>
/// (scope §5.1.1). A reply without the replied-to author takes it from the replied-to message's <c>create</c> row.
/// </para>
/// </remarks>
internal sealed partial class MessageRecorder(
    ApolloDbContext db,
    IPlatformIdResolver ids,
    ITrackingGate gate,
    RecordedEventMetrics metrics,
    ILogger<MessageRecorder> logger)
    : IPlatformEventHandler<PlatformMessageCreated>, IPlatformEventHandler<PlatformMessageUpdated>, IPlatformEventHandler<PlatformMessageDeleted>
{
    public async Task HandleAsync(PlatformMessageCreated platformEvent, CancellationToken cancellationToken)
    {
        if (gate.TryGetTrackedUserId(platformEvent.Author) is not { } userId)
        {
            Drop(platformEvent, platformEvent.Message);
            return;
        }

        var channel = await ResolveChannelAsync(platformEvent.Guild, platformEvent.Channel, cancellationToken);
        var replyToUserId = await ReplyToUserIdAsync(channel.GuildId, platformEvent, cancellationToken);

        db.MessageEvents.Add(Row(
            platformEvent, platformEvent.Message, MessageEventKind.Create, userId, channel, platformEvent.Metadata, replyToUserId, platformEvent.AuthorIsBot));
        foreach (var mentioned in platformEvent.MentionedUsers.Distinct())
        {
            db.MessageMentions.Add(new MessageMention
            {
                OccurredAt = platformEvent.OccurredAt,
                GuildId = channel.GuildId,
                ChannelId = channel.ChannelId,
                MessageExtId = platformEvent.Message.ExternalId,
                UserId = userId,
                MentionedUserId = gate.TryGetTrackedUserId(mentioned),
            });
        }

        await StoreAsync(platformEvent, platformEvent.Message, MessageEventKind.Create, userId, cancellationToken);
    }

    public Task HandleAsync(PlatformMessageUpdated platformEvent, CancellationToken cancellationToken) =>
        RecordFollowUpAsync(platformEvent, platformEvent.Channel, platformEvent.Message, platformEvent.Author, MessageEventKind.Edit, platformEvent.Metadata, cancellationToken);

    public Task HandleAsync(PlatformMessageDeleted platformEvent, CancellationToken cancellationToken) =>
        RecordFollowUpAsync(platformEvent, platformEvent.Channel, platformEvent.Message, platformEvent.Author, MessageEventKind.Delete, metadata: null, cancellationToken);

    /// <summary>
    /// Records an edit or delete. <paramref name="metadata"/> is <see langword="null"/> for deletes, which leaves the
    /// content columns <c>NULL</c>.
    /// </summary>
    private async Task RecordFollowUpAsync(
        PlatformEvent platformEvent,
        PlatformRef channelRef,
        PlatformRef message,
        PlatformRef? author,
        MessageEventKind kind,
        MessageMetadata? metadata,
        CancellationToken cancellationToken)
    {
        var authorId = author is null ? null : gate.TryGetTrackedUserId(author);
        if (author is not null && authorId is null)
        {
            Drop(platformEvent, message);
            return;
        }

        var channel = await ResolveChannelAsync(platformEvent.Guild, channelRef, cancellationToken);
        var created = await CreateRowAsync(channel.GuildId, message, cancellationToken);
        if ((authorId ?? created?.UserId) is not { } trackedId || !gate.IsTracked(trackedId))
        {
            Drop(platformEvent, message);
            return;
        }

        var isBot = created?.IsBot ?? await IsBotAsync(channel.GuildId, trackedId, cancellationToken);
        db.MessageEvents.Add(Row(platformEvent, message, kind, trackedId, channel, metadata, replyToUserId: null, isBot));
        await StoreAsync(platformEvent, message, kind, trackedId, cancellationToken);
    }

    private static MessageEvent Row(
        PlatformEvent platformEvent,
        PlatformRef message,
        MessageEventKind kind,
        Guid userId,
        ResolvedChannel channel,
        MessageMetadata? metadata,
        Guid? replyToUserId,
        bool isBot) => new()
        {
            GuildId = channel.GuildId,
            OccurredAt = platformEvent.OccurredAt,
            UserId = userId,
            ChannelId = channel.ChannelId,
            ThreadParentId = channel.ThreadParentId,
            MessageExtId = message.ExternalId,
            Kind = kind,
            Length = metadata?.Length,
            WordCount = metadata?.WordCount,
            AttachmentCount = metadata?.AttachmentCount,
            EmbedCount = metadata?.EmbedCount,
            EmojiCount = metadata?.EmojiCount,
            UserMentionCount = metadata?.UserMentionCount,
            RoleMentionCount = metadata?.RoleMentionCount,
            MentionsEveryone = metadata?.MentionsEveryone,
            ContentParts = metadata?.ContentParts,
            ReplyToUserId = replyToUserId,
            IsBot = isBot,
        };

    private async Task StoreAsync(PlatformEvent platformEvent, PlatformRef message, MessageEventKind kind, Guid userId, CancellationToken cancellationToken)
    {
        await db.SaveChangesAsync(cancellationToken);
        metrics.Recorded(platformEvent);
        LogRecorded(kind, message, userId);
    }

    private void Drop(PlatformEvent platformEvent, PlatformRef message)
    {
        metrics.Dropped(EventDropReason.Untracked);
        LogDropped(platformEvent.GetType().Name, message);
    }

    private async Task<ResolvedChannel> ResolveChannelAsync(PlatformRef guild, PlatformRef channel, CancellationToken cancellationToken)
    {
        var guildId = await ids.ResolveGuildAsync(guild, cancellationToken);
        var channelId = await ids.ResolveChannelAsync(guild, channel, cancellationToken);
        var threadParentId = await db.Channels
            .Where(c => c.Id == channelId)
            .Select(c => c.ThreadParentId)
            .SingleAsync(cancellationToken);
        return new ResolvedChannel(guildId, channelId, threadParentId);
    }

    /// <summary>
    /// The replied-to author if tracked: from the payload, else from our own <c>create</c> row of the replied-to message.
    /// Unknown or untracked authors are <see langword="null"/>.
    /// </summary>
    private async Task<Guid?> ReplyToUserIdAsync(Guid guildId, PlatformMessageCreated platformEvent, CancellationToken cancellationToken)
    {
        if (platformEvent.ReplyToAuthor is { } author)
        {
            return gate.TryGetTrackedUserId(author);
        }

        if (platformEvent.ReplyToMessage is not { } repliedTo)
        {
            return null;
        }

        var created = await CreateRowAsync(guildId, repliedTo, cancellationToken);
        return created is not null && gate.IsTracked(created.UserId) ? created.UserId : null;
    }

    private Task<CreateRow?> CreateRowAsync(Guid guildId, PlatformRef message, CancellationToken cancellationToken) =>
        db.MessageEvents
            .Where(e => e.GuildId == guildId && e.MessageExtId == message.ExternalId && e.Kind == MessageEventKind.Create)
            .Select(e => new CreateRow(e.UserId, e.IsBot))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Whether the member is a bot, for edits and deletes of messages without a <c>create</c> row (guild state sync, #13).
    /// </summary>
    private Task<bool> IsBotAsync(Guid guildId, Guid userId, CancellationToken cancellationToken) =>
        db.GuildMembers
            .Where(m => m.GuildId == guildId && m.UserId == userId)
            .Select(m => m.IsBot)
            .FirstOrDefaultAsync(cancellationToken);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Recorded message {Kind} of {MessageId} for {UserId}")]
    private partial void LogRecorded(MessageEventKind kind, PlatformRef messageId, Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped {EventType} of {MessageId}: author untracked or unknown")]
    private partial void LogDropped(string eventType, PlatformRef messageId);

    private sealed record ResolvedChannel(Guid GuildId, Guid ChannelId, Guid? ThreadParentId);

    private sealed record CreateRow(Guid UserId, bool IsBot);
}
