using Apollo.Data;
using Apollo.Data.Membership;
using Apollo.Stats.Raw;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Stats.Sanity;

/// <summary>
/// Runs the sanity checks of #40 over a Zurich date range, <c>[midnight of From, midnight after To)</c> as in #35, for one
/// guild. Rows count by their <c>occurred_at</c>, the start for sessions. Bots are not left out: a bot that was recorded
/// without being tracked is exactly what the untracked checks are for.
/// </summary>
public sealed class DataSanityService(ApolloDbContext db, TimeProvider time) : IDataSanityService
{
    /// <summary>
    /// A ban counts as tracked when the user's leave is at most this much older (<c>MembershipRecorder.BanGrace</c> in
    /// Apollo.Bot, #18): Discord sends the member remove of a ban first, and it closes the tracking period.
    /// </summary>
    private static readonly TimeSpan _banGrace = TimeSpan.FromSeconds(10);

    public async Task<DataSanityReport> GetAsync(DataSanityQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var range = StatsRange.ForZurichDays(query.From, query.To);

        // One context, so the checks run one after another.
        return new DataSanityReport(
            await HeartbeatGapsAsync(query, range, cancellationToken),
            await CheckAsync(LongSessions(query.GuildId, range), ToSession, cancellationToken),
            await CheckAsync(OpenSessions(query.GuildId, range), ToSession, cancellationToken),
            await CheckAsync(Untracked(query.GuildId, Events(query.GuildId, range)), ToUntracked, cancellationToken),
            await CheckAsync(Untracked(query.GuildId, References(query.GuildId, range)), ToUntracked, cancellationToken));
    }

    private async Task<SanityCheck<HeartbeatGap>> HeartbeatGapsAsync(DataSanityQuery query, StatsRange range, CancellationToken cancellationToken)
    {
        // Raw SQL: a gap is the distance to the previous heartbeat, a window function (LAG) that LINQ can't express; the
        // total comes from a second window over the gaps, so one round trip returns both. The last heartbeat before the
        // range and the first after it join in, so a gap that overlaps an edge of the range is reported with its real
        // start and end. This is the only raw query of the service.
        var rows = await db.Database.SqlQuery<GapRow>($"""
            WITH beats AS (
                SELECT occurred_at FROM bot_status
                WHERE guild_id = {query.GuildId} AND occurred_at >= {range.Start} AND occurred_at < {range.End}
                UNION ALL
                (SELECT occurred_at FROM bot_status
                WHERE guild_id = {query.GuildId} AND occurred_at < {range.Start}
                ORDER BY occurred_at DESC
                LIMIT 1)
                UNION ALL
                (SELECT occurred_at FROM bot_status
                WHERE guild_id = {query.GuildId} AND occurred_at >= {range.End}
                ORDER BY occurred_at
                LIMIT 1)
            ), pairs AS (
                SELECT LAG(occurred_at) OVER (ORDER BY occurred_at) AS previous_at, occurred_at
                FROM beats
            )
            SELECT previous_at AS "Start", occurred_at AS "End", COUNT(*) OVER () AS "Total"
            FROM pairs
            WHERE previous_at IS NOT NULL AND occurred_at - previous_at > {query.HeartbeatGapThreshold}
            ORDER BY previous_at DESC
            LIMIT {DataSanityQuery.MaxExamples}
            """).ToListAsync(cancellationToken);

        return rows.Count == 0
            ? SanityCheck<HeartbeatGap>.None
            : new SanityCheck<HeartbeatGap>((int)rows[0].Total, [.. rows.Select(r => new HeartbeatGap(r.Start, r.End))]);
    }

    /// <summary>Closed voice and presence sessions that started in the range and lasted longer than the threshold.</summary>
    private IQueryable<Row> LongSessions(Guid guildId, StatsRange range)
    {
        var threshold = DataSanityQuery.LongSessionThreshold;
        return db.VoiceSessions
            .Where(s => s.GuildId == guildId && s.OccurredAt >= range.Start && s.OccurredAt < range.End
                && s.EndedAt != null && s.EndedAt.Value - s.OccurredAt > threshold)
            .Select(s => new Row { Table = "voice_sessions", Column = "user_id", Id = s.Id, UserId = s.UserId, OccurredAt = s.OccurredAt, EndedAt = s.EndedAt, EndIncluded = false })
            .Concat(db.PresenceActivitySessions
                .Where(s => s.GuildId == guildId && s.OccurredAt >= range.Start && s.OccurredAt < range.End
                    && s.EndedAt - s.OccurredAt > threshold)
                .Select(s => new Row { Table = "presence_activity_sessions", Column = "user_id", Id = s.Id, UserId = s.UserId, OccurredAt = s.OccurredAt, EndedAt = s.EndedAt, EndIncluded = false }));
    }

    /// <summary>
    /// Voice sessions open for longer than the threshold now. An open session overlaps every range after its start, so one
    /// that started before the range counts too. Presence sessions are only written when they close (#19).
    /// </summary>
    private IQueryable<Row> OpenSessions(Guid guildId, StatsRange range)
    {
        var staleBefore = time.GetUtcNow() - DataSanityQuery.LongSessionThreshold;
        var startedBefore = staleBefore < range.End ? staleBefore : range.End;
        return db.VoiceSessions
            .Where(s => s.GuildId == guildId && s.EndedAt == null && s.OccurredAt < startedBefore)
            .Select(s => new Row { Table = "voice_sessions", Column = "user_id", Id = s.Id, UserId = s.UserId, OccurredAt = s.OccurredAt, EndedAt = null, EndIncluded = false });
    }

    /// <summary>The user of every row in the range, one source per event table (scope §5.1).</summary>
    private IQueryable<Row> Events(Guid guildId, StatsRange range) =>
        db.MessageEvents
            .Where(e => e.GuildId == guildId && e.OccurredAt >= range.Start && e.OccurredAt < range.End)
            .Select(e => new Row { Table = "message_events", Column = "user_id", Id = e.Id, UserId = e.UserId, OccurredAt = e.OccurredAt, EndedAt = null, EndIncluded = false })
            .Concat(db.MessageMentions
                .Where(e => e.GuildId == guildId && e.OccurredAt >= range.Start && e.OccurredAt < range.End)
                .Select(e => new Row { Table = "message_mentions", Column = "user_id", Id = e.Id, UserId = e.UserId, OccurredAt = e.OccurredAt, EndedAt = null, EndIncluded = false }))
            .Concat(db.ReactionEvents
                .Where(e => e.GuildId == guildId && e.OccurredAt >= range.Start && e.OccurredAt < range.End)
                .Select(e => new Row { Table = "reaction_events", Column = "user_id", Id = e.Id, UserId = e.UserId, OccurredAt = e.OccurredAt, EndedAt = null, EndIncluded = false }))
            .Concat(db.VoiceSessions
                .Where(e => e.GuildId == guildId && e.OccurredAt >= range.Start && e.OccurredAt < range.End)
                .Select(e => new Row { Table = "voice_sessions", Column = "user_id", Id = e.Id, UserId = e.UserId, OccurredAt = e.OccurredAt, EndedAt = null, EndIncluded = false }))
            .Concat(db.VoiceStateSegments
                .Where(e => e.GuildId == guildId && e.OccurredAt >= range.Start && e.OccurredAt < range.End)
                .Select(e => new Row { Table = "voice_state_segments", Column = "user_id", Id = e.Id, UserId = e.UserId, OccurredAt = e.OccurredAt, EndedAt = null, EndIncluded = false }))
            .Concat(db.MembershipEvents
                .Where(e => e.GuildId == guildId && e.OccurredAt >= range.Start && e.OccurredAt < range.End)
                // The recorder keeps a leave or ban that ends the tracking (both ends of the period inclusive), and a ban
                // up to 10 s after the leave that closed the period (#18).
                .Where(e => e.Kind != MembershipEventKind.Ban || !db.MembershipEvents.Any(l => l.GuildId == e.GuildId
                    && l.UserId == e.UserId
                    && l.Kind == MembershipEventKind.Leave
                    && l.OccurredAt <= e.OccurredAt
                    && l.OccurredAt >= e.OccurredAt - _banGrace))
                .Select(e => new Row
                {
                    Table = "membership_events",
                    Column = "user_id",
                    Id = e.Id,
                    UserId = e.UserId,
                    OccurredAt = e.OccurredAt,
                    EndedAt = null,
                    EndIncluded = e.Kind == MembershipEventKind.Leave || e.Kind == MembershipEventKind.Ban,
                }))
            .Concat(db.PresenceActivitySessions
                .Where(e => e.GuildId == guildId && e.OccurredAt >= range.Start && e.OccurredAt < range.End)
                .Select(e => new Row { Table = "presence_activity_sessions", Column = "user_id", Id = e.Id, UserId = e.UserId, OccurredAt = e.OccurredAt, EndedAt = null, EndIncluded = false }));

    /// <summary>Every non-NULL reference to another user in the range (scope §5.1.1: untracked ones are stored as NULL).</summary>
    private IQueryable<Row> References(Guid guildId, StatsRange range) =>
        db.MessageEvents
            .Where(e => e.GuildId == guildId && e.OccurredAt >= range.Start && e.OccurredAt < range.End && e.ReplyToUserId != null)
            .Select(e => new Row { Table = "message_events", Column = "reply_to_user_id", Id = e.Id, UserId = e.ReplyToUserId!.Value, OccurredAt = e.OccurredAt, EndedAt = null, EndIncluded = false })
            .Concat(db.MessageMentions
                .Where(e => e.GuildId == guildId && e.OccurredAt >= range.Start && e.OccurredAt < range.End && e.MentionedUserId != null)
                .Select(e => new Row { Table = "message_mentions", Column = "mentioned_user_id", Id = e.Id, UserId = e.MentionedUserId!.Value, OccurredAt = e.OccurredAt, EndedAt = null, EndIncluded = false }))
            .Concat(db.ReactionEvents
                .Where(e => e.GuildId == guildId && e.OccurredAt >= range.Start && e.OccurredAt < range.End && e.MessageAuthorId != null)
                .Select(e => new Row { Table = "reaction_events", Column = "message_author_id", Id = e.Id, UserId = e.MessageAuthorId!.Value, OccurredAt = e.OccurredAt, EndedAt = null, EndIncluded = false }));

    /// <summary>
    /// The rows whose user had no tracking period covering their time: <c>from &lt;= occurred_at AND (to IS NULL OR
    /// occurred_at &lt; to)</c>, the end inclusive where the recorder keeps it (<see cref="Row.EndIncluded"/>).
    /// </summary>
    private IQueryable<Row> Untracked(Guid guildId, IQueryable<Row> rows) =>
        rows.Where(r => !db.TrackingPeriods.Any(p => p.GuildId == guildId
            && p.UserId == r.UserId
            && p.From <= r.OccurredAt
            && (p.To == null || r.OccurredAt < p.To || (r.EndIncluded && r.OccurredAt == p.To))));

    private static async Task<SanityCheck<T>> CheckAsync<T>(IQueryable<Row> rows, Func<Row, T> map, CancellationToken cancellationToken)
    {
        var count = await rows.CountAsync(cancellationToken);
        if (count == 0)
        {
            return SanityCheck<T>.None;
        }

        var examples = await rows
            .OrderByDescending(r => r.OccurredAt)
            .ThenBy(r => r.Table)
            .ThenByDescending(r => r.Id)
            .Take(DataSanityQuery.MaxExamples)
            .ToListAsync(cancellationToken);
        return new SanityCheck<T>(count, [.. examples.Select(map)]);
    }

    private static SanitySession ToSession(Row row) => new(row.Table, row.Id, row.UserId, row.OccurredAt, row.EndedAt);

    private static UntrackedRow ToUntracked(Row row) => new(row.Table, row.Column, row.Id, row.UserId, row.OccurredAt);

    /// <summary>One row of any table, so the tables of a check form one query (<c>UNION ALL</c>).</summary>
    private sealed class Row
    {
        public required string Table { get; init; }

        public required string Column { get; init; }

        public long Id { get; init; }

        public Guid UserId { get; init; }

        public DateTimeOffset OccurredAt { get; init; }

        public DateTimeOffset? EndedAt { get; init; }

        /// <summary>Whether a tracking period that ends at <see cref="OccurredAt"/> still covers the row.</summary>
        public bool EndIncluded { get; init; }
    }

    private sealed record GapRow(DateTimeOffset Start, DateTimeOffset End, long Total);
}
