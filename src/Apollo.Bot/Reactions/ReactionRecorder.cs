using Apollo.Bot.Tracking;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Messages;
using Apollo.Data.Reactions;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Reactions;

/// <summary>
/// Records reactions added or removed by tracked users into <c>reaction_events</c> (#17, scope §5.1).
/// </summary>
/// <remarks>
/// <para>The tracking gate decides by the reacting user; nothing about an untracked reactor is stored.</para>
/// <para>
/// The message author is stored only if tracked, else <c>NULL</c> (scope §5.1.1), and never creates a user row. When
/// the event has no author (Discord's remove never has one, Fluxer sends none at all), it comes from the latest earlier
/// <c>add</c> row of the same user, message and emoji, else from the message's <c>create</c> row in
/// <c>message_events</c>; without either it is <c>NULL</c>.
/// </para>
/// </remarks>
internal sealed partial class ReactionRecorder(
    ApolloDbContext db,
    IPlatformIdResolver ids,
    ITrackingGate gate,
    RecordedEventMetrics metrics,
    ILogger<ReactionRecorder> logger)
    : IPlatformEventHandler<PlatformReactionAdded>, IPlatformEventHandler<PlatformReactionRemoved>
{
    public Task HandleAsync(PlatformReactionAdded platformEvent, CancellationToken cancellationToken) =>
        RecordAsync(
            platformEvent, platformEvent.Channel, platformEvent.Message, platformEvent.User, platformEvent.MessageAuthor,
            platformEvent.Emoji, ReactionEventKind.Add, cancellationToken);

    public Task HandleAsync(PlatformReactionRemoved platformEvent, CancellationToken cancellationToken) =>
        RecordAsync(
            platformEvent, platformEvent.Channel, platformEvent.Message, platformEvent.User, platformEvent.MessageAuthor,
            platformEvent.Emoji, ReactionEventKind.Remove, cancellationToken);

    private async Task RecordAsync(
        PlatformEvent platformEvent,
        PlatformRef channel,
        PlatformRef message,
        PlatformRef user,
        PlatformRef? messageAuthor,
        PlatformEmoji emoji,
        ReactionEventKind kind,
        CancellationToken cancellationToken)
    {
        if (gate.TryGetTrackedUserId(user) is not { } userId)
        {
            metrics.Dropped(EventDropReason.Untracked);
            LogDropped(platformEvent.GetType().Name, message);
            return;
        }

        var guildId = await ids.ResolveGuildAsync(platformEvent.Guild, cancellationToken);
        var channelId = await ids.ResolveChannelAsync(platformEvent.Guild, channel, cancellationToken);
        var emojiCustomId = emoji.CustomEmoji?.ExternalId;
        var messageAuthorId = messageAuthor is not null
            ? gate.TryGetTrackedUserId(messageAuthor)
            : await LookUpMessageAuthorIdAsync(guildId, message, userId, emoji.Name, emojiCustomId, cancellationToken);

        db.ReactionEvents.Add(new ReactionEvent
        {
            GuildId = guildId,
            OccurredAt = platformEvent.OccurredAt,
            UserId = userId,
            ChannelId = channelId,
            MessageExtId = message.ExternalId,
            MessageAuthorId = messageAuthorId,
            EmojiName = emoji.Name,
            EmojiCustomId = emojiCustomId,
            Kind = kind,
        });
        await db.SaveChangesAsync(cancellationToken);
        metrics.Recorded(platformEvent);
        LogRecorded(kind, message, userId);
    }

    /// <summary>
    /// The message author for an event without one, if tracked: from the latest earlier <c>add</c> row of the same
    /// reaction, else from the message's <c>create</c> row. A custom emoji matches by its id (its name may change), a
    /// unicode emoji by itself.
    /// </summary>
    private async Task<Guid?> LookUpMessageAuthorIdAsync(
        Guid guildId,
        PlatformRef message,
        Guid userId,
        string emojiName,
        string? emojiCustomId,
        CancellationToken cancellationToken)
    {
        var addRows = db.ReactionEvents.Where(e =>
            e.GuildId == guildId
            && e.MessageExtId == message.ExternalId
            && e.UserId == userId
            && e.Kind == ReactionEventKind.Add
            && e.MessageAuthorId != null);
        addRows = emojiCustomId is null
            ? addRows.Where(e => e.EmojiCustomId == null && e.EmojiName == emojiName)
            : addRows.Where(e => e.EmojiCustomId == emojiCustomId);

        var authorId = await addRows
                .OrderByDescending(e => e.OccurredAt)
                .Select(e => e.MessageAuthorId)
                .FirstOrDefaultAsync(cancellationToken)
            ?? await db.MessageEvents
                .Where(e => e.GuildId == guildId && e.MessageExtId == message.ExternalId && e.Kind == MessageEventKind.Create)
                .Select(e => (Guid?)e.UserId)
                .FirstOrDefaultAsync(cancellationToken);

        return authorId is { } id && gate.IsTracked(id) ? id : null;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Recorded reaction {Kind} on {MessageId} for {UserId}")]
    private partial void LogRecorded(ReactionEventKind kind, PlatformRef messageId, Guid userId);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Dropped {EventType} on {MessageId}: user untracked")]
    private partial void LogDropped(string eventType, PlatformRef messageId);
}
