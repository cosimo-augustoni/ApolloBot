using Apollo.Data;
using Apollo.Data.Messages;
using Apollo.Data.Reactions;
using Apollo.Domain.Presence;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Stats.Raw;

/// <summary>
/// Reads the raw event tables (#15–#17, #19) for a Zurich date range, <c>[midnight of From, midnight after To)</c> as
/// in #35. Bots (a <c>guild_members</c> row with <c>is_bot</c>) are left out unless the query includes them.
/// </summary>
public sealed class RawStatsService(ApolloDbContext db) : IRawStatsService
{
    public async Task<IReadOnlyList<VoiceBuddy>> GetVoiceBuddiesAsync(RawStatsQuery query, CancellationToken cancellationToken)
    {
        var range = RangeOf(query);
        var forUser = query.UserId is not null;
        var userId = query.UserId ?? Guid.Empty;

        // Raw SQL: the overlap of two sessions clipped to the range is a self-join with GREATEST/LEAST over three
        // instants, which LINQ can't express. This is the only raw query of the service. Both sessions must be closed
        // (open ones are ignored, #36) and in the same channel; for the guild each pair appears once (a.user_id <
        // b.user_id), for a user they are always a. Ties break by the IDs, so the order is stable.
        return await db.Database.SqlQuery<VoiceBuddy>($"""
            SELECT a.user_id AS "UserId", b.user_id AS "BuddyId",
                SUM(EXTRACT(EPOCH FROM
                    LEAST(a.ended_at, b.ended_at, {range.End}) - GREATEST(a.occurred_at, b.occurred_at, {range.Start})
                ))::bigint AS "OverlapSeconds"
            FROM voice_sessions a
            JOIN voice_sessions b
                ON b.guild_id = a.guild_id
                AND b.channel_id = a.channel_id
                AND b.user_id <> a.user_id
                AND b.ended_at IS NOT NULL
                AND b.occurred_at < LEAST(a.ended_at, {range.End})
                AND b.ended_at > GREATEST(a.occurred_at, {range.Start})
            WHERE a.guild_id = {query.GuildId}
                AND a.ended_at IS NOT NULL
                AND a.occurred_at < {range.End}
                AND a.ended_at > {range.Start}
                AND (({forUser} AND a.user_id = {userId}) OR (NOT {forUser} AND a.user_id < b.user_id))
                AND ({query.IncludeBots} OR NOT EXISTS (
                    SELECT 1 FROM guild_members m
                    WHERE m.guild_id = a.guild_id AND m.user_id IN (a.user_id, b.user_id) AND m.is_bot))
            GROUP BY a.user_id, b.user_id
            ORDER BY "OverlapSeconds" DESC, a.user_id, b.user_id
            LIMIT {query.Top}
            """).ToListAsync(cancellationToken);
    }

    public async Task<ReplyGraph> GetReplyGraphAsync(RawStatsQuery query, CancellationToken cancellationToken)
    {
        var range = RangeOf(query);
        var replies = db.MessageEvents.AsNoTracking()
            .Where(m => m.GuildId == query.GuildId
                && m.Kind == MessageEventKind.Create
                && m.ReplyToUserId != null
                && m.ReplyToUserId != m.UserId
                && m.OccurredAt >= range.Start
                && m.OccurredAt < range.End);
        if (query.UserId is { } userId)
        {
            replies = replies.Where(m => m.UserId == userId || m.ReplyToUserId == userId);
        }

        if (!query.IncludeBots)
        {
            // The message's own flag, and guild_members for both ends, as #35 decides who is a bot.
            replies = replies.Where(m => !m.IsBot
                && !db.GuildMembers.Any(g => g.GuildId == m.GuildId && (g.UserId == m.UserId || g.UserId == m.ReplyToUserId) && g.IsBot));
        }

        // One row per directed edge, so few enough to order in memory, where Guid ties break the same way every time.
        var edges = await replies
            .GroupBy(m => new { m.UserId, RepliedToId = m.ReplyToUserId!.Value })
            .Select(g => new ReplyEdge(g.Key.UserId, g.Key.RepliedToId, g.Count()))
            .ToListAsync(cancellationToken);
        IReadOnlyList<ReplyEdge> Top(Func<ReplyEdge, bool> filter) =>
            [.. edges.Where(filter).OrderByDescending(e => e.Count).ThenBy(e => e.AuthorId).ThenBy(e => e.RepliedToId).Take(query.Top)];

        return query.UserId is { } user
            ? new ReplyGraph(Top(_ => true), Top(e => e.AuthorId == user), Top(e => e.RepliedToId == user))
            : new ReplyGraph(Top(_ => true), [], []);
    }

    public async Task<LongestVoiceSession?> GetLongestVoiceSessionAsync(RawStatsQuery query, CancellationToken cancellationToken)
    {
        var range = RangeOf(query);
        var sessions = db.VoiceSessions.AsNoTracking()
            .Where(s => s.GuildId == query.GuildId && s.EndedAt != null && s.OccurredAt >= range.Start && s.OccurredAt < range.End);
        if (query.UserId is { } userId)
        {
            sessions = sessions.Where(s => s.UserId == userId);
        }

        if (!query.IncludeBots)
        {
            sessions = sessions.Where(s => !db.GuildMembers.Any(g => g.GuildId == s.GuildId && g.UserId == s.UserId && g.IsBot));
        }

        var longest = await sessions
            .OrderByDescending(s => s.EndedAt!.Value - s.OccurredAt)
            .ThenBy(s => s.OccurredAt)
            .ThenBy(s => s.UserId)
            .Select(s => new { s.UserId, s.ChannelId, s.OccurredAt, EndedAt = s.EndedAt!.Value })
            .FirstOrDefaultAsync(cancellationToken);

        return longest is null ? null : new LongestVoiceSession(longest.UserId, longest.ChannelId, longest.OccurredAt, longest.EndedAt - longest.OccurredAt);
    }

    public async Task<IReadOnlyList<EmojiUsage>> GetTopEmojisAsync(RawStatsQuery query, CancellationToken cancellationToken)
    {
        var range = RangeOf(query);
        var reactions = db.ReactionEvents.AsNoTracking()
            .Where(r => r.GuildId == query.GuildId && r.Kind == ReactionEventKind.Add && r.OccurredAt >= range.Start && r.OccurredAt < range.End);
        if (query.UserId is { } userId)
        {
            reactions = reactions.Where(r => r.UserId == userId);
        }

        if (!query.IncludeBots)
        {
            // reaction_events has no bot flag (#17): the reacting user's guild_members row decides.
            reactions = reactions.Where(r => !db.GuildMembers.Any(g => g.GuildId == r.GuildId && g.UserId == r.UserId && g.IsBot));
        }

        // A custom emoji keeps its ID but may be renamed (or lose its name, #17), so the names are merged per ID in
        // memory: one row per emoji and name.
        var names = await reactions
            .GroupBy(r => new { r.EmojiCustomId, r.EmojiName })
            .Select(g => new { g.Key.EmojiCustomId, g.Key.EmojiName, Count = g.Count(), LastUsed = g.Max(r => r.OccurredAt) })
            .ToListAsync(cancellationToken);

        return
        [
            .. names
                .GroupBy(n => n.EmojiCustomId is null ? (Unicode: true, Key: n.EmojiName) : (Unicode: false, Key: n.EmojiCustomId))
                .Select(g => new EmojiUsage(
                    g.OrderByDescending(n => n.EmojiName.Length > 0).ThenByDescending(n => n.LastUsed).ThenBy(n => n.EmojiName, StringComparer.Ordinal).First().EmojiName,
                    g.Key.Unicode ? null : g.Key.Key,
                    g.Sum(n => n.Count)))
                .OrderByDescending(e => e.Count)
                .ThenBy(e => e.Name, StringComparer.Ordinal)
                .ThenBy(e => e.CustomId is not null)
                .ThenBy(e => e.CustomId, StringComparer.Ordinal)
                .Take(query.Top),
        ];
    }

    public async Task<IReadOnlyList<GamePlaytime>> GetTopGamesAsync(RawStatsQuery query, CancellationToken cancellationToken)
    {
        var range = RangeOf(query);
        var sessions = db.PresenceActivitySessions.AsNoTracking()
            .Where(s => s.GuildId == query.GuildId
                && s.ActivityType == PresenceActivityType.Playing
                && s.OccurredAt < range.End
                && s.EndedAt > range.Start);
        if (query.UserId is { } userId)
        {
            sessions = sessions.Where(s => s.UserId == userId);
        }

        if (!query.IncludeBots)
        {
            sessions = sessions.Where(s => !db.GuildMembers.Any(g => g.GuildId == s.GuildId && g.UserId == s.UserId && g.IsBot));
        }

        // Sessions are clipped to the range in memory (StatsRange.Clip); a member has a few per day at most.
        var rows = await sessions
            .Select(s => new { s.ActivityName, s.OccurredAt, s.EndedAt })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows
                .GroupBy(r => r.ActivityName, StringComparer.Ordinal)
                .Select(g => new GamePlaytime(g.Key, g.Sum(r => (long)range.Clip(r.OccurredAt, r.EndedAt).TotalSeconds)))
                .OrderByDescending(g => g.Seconds)
                .ThenBy(g => g.Name, StringComparer.Ordinal)
                .Take(query.Top),
        ];
    }

    private static StatsRange RangeOf(RawStatsQuery query)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Top, 1, nameof(query));
        return StatsRange.ForZurichDays(query.From, query.To);
    }
}
