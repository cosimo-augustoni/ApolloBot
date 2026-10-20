using Apollo.Data;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Privacy;

/// <summary>
/// Deletes what the recorders stored about a user (#22, scope §5.4): their rows in every event table (scope §5.1) and in
/// <c>tracking_periods</c>, and clears the references other users' rows hold to them (<c>reply_to_user_id</c>,
/// <c>mentioned_user_id</c>, <c>message_author_id</c>). Reference tables (<c>users</c>, <c>guild_members</c>,
/// <c>member_roles</c>) stay: the role sync and access checks need them (scope §5.1.1).
/// </summary>
/// <remarks>
/// A recorder issue that adds an event table adds it here too, like in <c>UserDataSummary</c>. The tables are
/// hypertables; a plain <c>DELETE</c> works on uncompressed chunks (no compression in M1). Its range
/// (<see cref="FindRangeAsync"/>) covers every event table it changes, also those no continuous aggregate reads yet.
/// </remarks>
internal sealed class RecordedDataEraserStep(ApolloDbContext db) : IUserDataEraserStep
{
    public async Task<ErasedRange?> FindRangeAsync(Guid guildId, Guid userId, CancellationToken cancellationToken)
    {
        ErasedRange?[] ranges =
        [
            await RangeAsync(db.MessageEvents.Where(e => e.GuildId == guildId && (e.UserId == userId || e.ReplyToUserId == userId)).Select(e => e.OccurredAt), cancellationToken),
            await RangeAsync(db.MessageMentions.Where(e => e.GuildId == guildId && (e.UserId == userId || e.MentionedUserId == userId)).Select(e => e.OccurredAt), cancellationToken),
            await RangeAsync(db.VoiceSessions.Where(e => e.GuildId == guildId && e.UserId == userId).Select(e => e.OccurredAt), cancellationToken),
            await RangeAsync(db.VoiceStateSegments.Where(e => e.GuildId == guildId && e.UserId == userId).Select(e => e.OccurredAt), cancellationToken),
            await RangeAsync(db.ReactionEvents.Where(e => e.GuildId == guildId && (e.UserId == userId || e.MessageAuthorId == userId)).Select(e => e.OccurredAt), cancellationToken),
            await RangeAsync(db.MembershipEvents.Where(e => e.GuildId == guildId && e.UserId == userId).Select(e => e.OccurredAt), cancellationToken),
            await RangeAsync(db.PresenceActivitySessions.Where(e => e.GuildId == guildId && e.UserId == userId).Select(e => e.OccurredAt), cancellationToken),
            await RangeAsync(db.PresenceActivitySegments.Where(e => e.GuildId == guildId && e.UserId == userId).Select(e => e.OccurredAt), cancellationToken),
            await RangeAsync(db.MemberNameChanges.Where(e => e.GuildId == guildId && e.UserId == userId).Select(e => e.OccurredAt), cancellationToken),
        ];

        return ranges.OfType<ErasedRange>().Aggregate((ErasedRange?)null, (union, range) => union?.Union(range) ?? range);
    }

    public async Task<IReadOnlyList<ErasedRows>> EraseAsync(Guid guildId, Guid userId, CancellationToken cancellationToken) =>
    [
        new(
            "message_events",
            await db.MessageEvents.Where(e => e.GuildId == guildId && e.UserId == userId).ExecuteDeleteAsync(cancellationToken),
            await db.MessageEvents
                .Where(e => e.GuildId == guildId && e.ReplyToUserId == userId)
                .ExecuteUpdateAsync(e => e.SetProperty(x => x.ReplyToUserId, (Guid?)null), cancellationToken)),
        new(
            "message_mentions",
            await db.MessageMentions.Where(e => e.GuildId == guildId && e.UserId == userId).ExecuteDeleteAsync(cancellationToken),
            await db.MessageMentions
                .Where(e => e.GuildId == guildId && e.MentionedUserId == userId)
                .ExecuteUpdateAsync(e => e.SetProperty(x => x.MentionedUserId, (Guid?)null), cancellationToken)),
        new(
            "voice_sessions",
            await db.VoiceSessions.Where(e => e.GuildId == guildId && e.UserId == userId).ExecuteDeleteAsync(cancellationToken)),
        new(
            "voice_state_segments",
            await db.VoiceStateSegments.Where(e => e.GuildId == guildId && e.UserId == userId).ExecuteDeleteAsync(cancellationToken)),
        new(
            "reaction_events",
            await db.ReactionEvents.Where(e => e.GuildId == guildId && e.UserId == userId).ExecuteDeleteAsync(cancellationToken),
            await db.ReactionEvents
                .Where(e => e.GuildId == guildId && e.MessageAuthorId == userId)
                .ExecuteUpdateAsync(e => e.SetProperty(x => x.MessageAuthorId, (Guid?)null), cancellationToken)),
        new(
            "membership_events",
            await db.MembershipEvents.Where(e => e.GuildId == guildId && e.UserId == userId).ExecuteDeleteAsync(cancellationToken)),
        new(
            "presence_activity_sessions",
            await db.PresenceActivitySessions.Where(e => e.GuildId == guildId && e.UserId == userId).ExecuteDeleteAsync(cancellationToken)),
        new(
            "presence_activity_segments",
            await db.PresenceActivitySegments.Where(e => e.GuildId == guildId && e.UserId == userId).ExecuteDeleteAsync(cancellationToken)),
        new(
            "member_name_changes",
            await db.MemberNameChanges.Where(e => e.GuildId == guildId && e.UserId == userId).ExecuteDeleteAsync(cancellationToken)),
        new(
            "tracking_periods",
            await db.TrackingPeriods.Where(p => p.GuildId == guildId && p.UserId == userId).ExecuteDeleteAsync(cancellationToken)),
    ];

    private static async Task<ErasedRange?> RangeAsync(IQueryable<DateTimeOffset> occurredAt, CancellationToken cancellationToken)
    {
        var nullable = occurredAt.Select(at => (DateTimeOffset?)at);
        return await nullable.MinAsync(cancellationToken) is { } from && await nullable.MaxAsync(cancellationToken) is { } to
            ? new ErasedRange(from, to)
            : null;
    }
}
