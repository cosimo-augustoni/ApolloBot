using Apollo.Bot.Tests.Tracking;
using Apollo.Data.Membership;
using Apollo.Data.Messages;
using Apollo.Data.Presence;
using Apollo.Data.Reactions;
using Apollo.Data.Tracking;
using Apollo.Data.Voice;
using Apollo.Domain.Presence;
using Apollo.Domain.Voice;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Tests.Privacy;

/// <summary>
/// Seeds and reads what the bot records about users, in every event table and in <c>tracking_periods</c>.
/// </summary>
internal static class RecordedData
{
    public const string ReplyReferences = "message_events.reply_to_user_id";
    public const string MentionReferences = "message_mentions.mentioned_user_id";
    public const string ReactionReferences = "reaction_events.message_author_id";

    /// <summary>
    /// Seeds one row of <paramref name="user"/> in every event table, plus a closed earlier tracking period. Their
    /// message replies to and mentions <paramref name="referenced"/>, and their reaction is on a message of
    /// <paramref name="referenced"/>. Every row starts <paramref name="at"/>, by default three days before
    /// <see cref="TestTime.Now"/> (long before the database's own clock, so outside every aggregate refresh policy window).
    /// </summary>
    public static Task SeedAsync(TrackingHost host, PlatformRef user, PlatformRef referenced, DateTimeOffset? at = null) =>
        host.InScopeAsync(async (db, ids) =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var guildId = await ids.ResolveGuildAsync(TestPlatform.Guild, cancellationToken);
            var userId = await ids.ResolveUserAsync(user, cancellationToken);
            var referencedId = await ids.ResolveUserAsync(referenced, cancellationToken);
            var channelId = await ids.ResolveChannelAsync(TestPlatform.Guild, TestPlatform.Channel("general"), cancellationToken);
            var occurredAt = at ?? TestTime.Now - TimeSpan.FromDays(3);
            var message = $"message-of-{user.ExternalId}";

            db.MessageEvents.Add(new MessageEvent
            {
                GuildId = guildId,
                UserId = userId,
                ChannelId = channelId,
                OccurredAt = occurredAt,
                MessageExtId = message,
                Kind = MessageEventKind.Create,
                Length = 42,
                ReplyToUserId = referencedId,
            });
            db.MessageMentions.Add(new MessageMention
            {
                GuildId = guildId,
                UserId = userId,
                ChannelId = channelId,
                OccurredAt = occurredAt,
                MessageExtId = message,
                MentionedUserId = referencedId,
            });
            db.ReactionEvents.Add(new ReactionEvent
            {
                GuildId = guildId,
                UserId = userId,
                ChannelId = channelId,
                OccurredAt = occurredAt,
                MessageExtId = $"message-of-{referenced.ExternalId}",
                MessageAuthorId = referencedId,
                EmojiName = "👍",
                Kind = ReactionEventKind.Add,
            });
            db.MembershipEvents.Add(new MembershipEvent { GuildId = guildId, UserId = userId, OccurredAt = occurredAt, Kind = MembershipEventKind.Join });
            db.PresenceActivitySessions.Add(new PresenceActivitySession
            {
                GuildId = guildId,
                UserId = userId,
                OccurredAt = occurredAt,
                EndedAt = occurredAt.AddHours(1),
                ActivityType = PresenceActivityType.Playing,
                ActivityName = "Factorio",
            });
            db.TrackingPeriods.Add(new TrackingPeriod { GuildId = guildId, UserId = userId, From = occurredAt.AddDays(-1), To = occurredAt.AddDays(1) });
            var session = new VoiceSession
            {
                GuildId = guildId,
                UserId = userId,
                ChannelId = channelId,
                OccurredAt = occurredAt,
                EndedAt = occurredAt.AddMinutes(30),
                EndReason = VoiceSessionEndReason.Left,
            };
            db.VoiceSessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);
            db.VoiceStateSegments.Add(new VoiceStateSegment
            {
                GuildId = guildId,
                UserId = userId,
                SessionId = session.Id,
                OccurredAt = occurredAt,
                EndedAt = occurredAt.AddMinutes(30),
            });
            return await db.SaveChangesAsync(cancellationToken);
        });

    /// <summary>
    /// The rows about <paramref name="user"/>: their own rows per table, and the rows of anyone that reference them
    /// (<see cref="ReplyReferences"/>, <see cref="MentionReferences"/>, <see cref="ReactionReferences"/>).
    /// </summary>
    public static Task<Dictionary<string, int>> RowsAboutAsync(TrackingHost host, PlatformRef user) =>
        host.InScopeAsync(async (db, ids) =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var id = await ids.TryGetUserAsync(user, cancellationToken) ?? throw new InvalidOperationException($"{user} was never resolved.");
            return new Dictionary<string, int>
            {
                ["message_events"] = await db.MessageEvents.CountAsync(e => e.UserId == id, cancellationToken),
                ["message_mentions"] = await db.MessageMentions.CountAsync(e => e.UserId == id, cancellationToken),
                ["voice_sessions"] = await db.VoiceSessions.CountAsync(e => e.UserId == id, cancellationToken),
                ["voice_state_segments"] = await db.VoiceStateSegments.CountAsync(e => e.UserId == id, cancellationToken),
                ["reaction_events"] = await db.ReactionEvents.CountAsync(e => e.UserId == id, cancellationToken),
                ["membership_events"] = await db.MembershipEvents.CountAsync(e => e.UserId == id, cancellationToken),
                ["presence_activity_sessions"] = await db.PresenceActivitySessions.CountAsync(e => e.UserId == id, cancellationToken),
                ["tracking_periods"] = await db.TrackingPeriods.CountAsync(e => e.UserId == id, cancellationToken),
                [ReplyReferences] = await db.MessageEvents.CountAsync(e => e.ReplyToUserId == id, cancellationToken),
                [MentionReferences] = await db.MessageMentions.CountAsync(e => e.MentionedUserId == id, cancellationToken),
                [ReactionReferences] = await db.ReactionEvents.CountAsync(e => e.MessageAuthorId == id, cancellationToken),
            };
        });

    /// <summary>
    /// Every row of <paramref name="user"/> in the tables with references to other users, as text without the reference
    /// columns, to check that clearing a reference changed nothing else.
    /// </summary>
    public static Task<List<string>> RowsWithoutReferencesAsync(TrackingHost host, PlatformRef user) =>
        host.InScopeAsync(async (db, ids) =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var id = await ids.TryGetUserAsync(user, cancellationToken) ?? throw new InvalidOperationException($"{user} was never resolved.");
            var messages = await db.MessageEvents.AsNoTracking().Where(e => e.UserId == id).OrderBy(e => e.Id)
                .Select(e => new { e.Id, e.OccurredAt, e.GuildId, e.UserId, e.ChannelId, e.MessageExtId, e.Kind, e.Length })
                .ToListAsync(cancellationToken);
            var mentions = await db.MessageMentions.AsNoTracking().Where(e => e.UserId == id).OrderBy(e => e.Id)
                .Select(e => new { e.Id, e.OccurredAt, e.GuildId, e.UserId, e.ChannelId, e.MessageExtId })
                .ToListAsync(cancellationToken);
            var reactions = await db.ReactionEvents.AsNoTracking().Where(e => e.UserId == id).OrderBy(e => e.Id)
                .Select(e => new { e.Id, e.OccurredAt, e.GuildId, e.UserId, e.ChannelId, e.MessageExtId, e.EmojiName, e.Kind })
                .ToListAsync(cancellationToken);
            return messages.Select(r => $"{r}").Concat(mentions.Select(r => $"{r}")).Concat(reactions.Select(r => $"{r}")).ToList();
        });
}
