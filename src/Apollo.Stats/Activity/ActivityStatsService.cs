using System.Linq.Expressions;
using Apollo.Data;
using Apollo.Data.Activity;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Stats.Activity;

/// <summary>
/// Reads <c>daily_user_channel_activity</c> and <c>hourly_activity_heatmap</c> (#34), <c>tracking_periods</c> and, for the
/// voice heatmap, <c>voice_sessions</c> (#111) for a Zurich date range. The Zurich days become the UTC instants of their
/// midnights, which is what the aggregates' buckets hold, so the range is <c>[midnight of From, midnight after To)</c>.
/// </summary>
public sealed class ActivityStatsService(ApolloDbContext db, TimeProvider time) : IActivityStatsService
{
    /// <summary>
    /// What a user did themselves: a message, a reaction or voice time. Reactions others gave them do not count.
    /// </summary>
    private static readonly Expression<Func<DailyUserChannelActivity, bool>> _isActive =
        a => a.MessageCount > 0 || a.ReactionsGiven > 0 || a.VoiceSeconds > 0;

    public async Task<ActivityStatsDto> GetAsync(ActivityStatsQuery query, CancellationToken cancellationToken)
    {
        if (query.From > query.To)
        {
            throw new ArgumentException($"From ({query.From:O}) must not be after To ({query.To:O}).", nameof(query));
        }

        var start = ZurichCalendar.StartOf(query.From);
        var end = ZurichCalendar.StartOf(query.To.AddDays(1));
        var activity = Activity(query, start, end);

        var days = await activity
            .GroupBy(a => a.Day)
            .Select(g => new
            {
                Day = g.Key,
                Counts = new ActivityCounts(
                    g.Sum(a => a.MessageCount),
                    g.Sum(a => a.CharacterCount),
                    g.Sum(a => a.ReactionsGiven),
                    g.Sum(a => a.ReactionsReceived),
                    g.Sum(a => a.VoiceSeconds)),
            })
            .ToListAsync(cancellationToken);
        var countsByDate = days.ToDictionary(d => ZurichCalendar.DayOf(d.Day), d => d.Counts);
        var daily = Dates(query.From, query.To)
            .Select(date => new DailyActivity(date, countsByDate.GetValueOrDefault(date, ActivityCounts.Zero)))
            .ToList();
        var totals = Sum(daily.Select(d => d.Counts));

        int? activeUsers = query.UserId is null
            ? await activity.Where(_isActive).Select(a => a.UserId).Distinct().CountAsync(cancellationToken)
            : null;

        int? trackedDays = null;
        DateOnly? trackedSince = null;
        if (query.UserId is { } userId)
        {
            var periods = await db.TrackingPeriods.AsNoTracking()
                .Where(p => p.GuildId == query.GuildId && p.UserId == userId)
                .Select(p => new TrackingSpan(p.From, p.To))
                .ToListAsync(cancellationToken);
            trackedDays = TrackedDays.Count(periods, query.From, query.To, time.GetUtcNow());
            // The open period (at most one per member): "Erfasst seit …" means since when they are tracked now.
            trackedSince = periods.Where(p => p.To is null).Select(p => (DateOnly?)ZurichCalendar.DayOf(p.From)).SingleOrDefault();
        }

        return new ActivityStatsDto
        {
            From = query.From,
            To = query.To,
            Totals = totals,
            Averages = Average(totals, trackedDays ?? daily.Count),
            ActiveDays = daily.Count(d => d.Counts.IsActive),
            ActiveUsers = activeUsers,
            TrackedDays = trackedDays,
            TrackedSince = trackedSince,
            Daily = daily,
            Channels = await ChannelsAsync(activity, cancellationToken),
            Heatmap = await HeatmapAsync(query, start, end, cancellationToken),
            VoiceHeatmap = await VoiceHeatmapAsync(query, start, end, cancellationToken),
        };
    }

    private IQueryable<DailyUserChannelActivity> Activity(ActivityStatsQuery query, DateTimeOffset start, DateTimeOffset end)
    {
        var rows = db.DailyUserChannelActivity.AsNoTracking()
            .Where(a => a.GuildId == query.GuildId && a.Day >= start && a.Day < end);
        if (query.UserId is { } userId)
        {
            rows = rows.Where(a => a.UserId == userId);
        }

        // The view's is_bot already combines the message rows and guild_members (#34).
        return query.IncludeBots ? rows : rows.Where(a => !a.IsBot);
    }

    private async Task<IReadOnlyList<ChannelActivity>> ChannelsAsync(IQueryable<DailyUserChannelActivity> activity, CancellationToken cancellationToken)
    {
        // Threads roll up to their parent channel (D2); the parent comes from channels, joined in the view.
        var channels = await activity
            .Where(a => a.MessageCount > 0 || a.VoiceSeconds > 0)
            .GroupBy(a => a.ThreadParentId ?? a.ChannelId)
            .Select(g => new { ChannelId = g.Key, Messages = g.Sum(a => a.MessageCount), VoiceSeconds = g.Sum(a => a.VoiceSeconds) })
            .Join(
                db.Channels,
                a => a.ChannelId,
                c => c.Id,
                (a, c) => new ChannelActivity(a.ChannelId, c.Name, c.DeletedAt != null, a.Messages, a.VoiceSeconds))
            .ToListAsync(cancellationToken);

        return [.. channels.OrderByDescending(c => c.Messages).ThenByDescending(c => c.VoiceSeconds).ThenBy(c => c.Name, StringComparer.Ordinal)];
    }

    private async Task<IReadOnlyList<IReadOnlyList<long>>> HeatmapAsync(
        ActivityStatsQuery query, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var rows = db.HourlyActivityHeatmap.AsNoTracking()
            .Where(h => h.GuildId == query.GuildId && h.Hour >= start && h.Hour < end);
        if (query.UserId is { } userId)
        {
            rows = rows.Where(h => h.UserId == userId);
        }

        if (!query.IncludeBots)
        {
            // The heatmap's is_bot has only the message rows' flag; guild_members too, as in daily_user_channel_activity,
            // so tiles and heatmap leave out the same bots.
            rows = rows.Where(h => !h.IsBot && !db.GuildMembers.Any(m => m.GuildId == h.GuildId && m.UserId == h.UserId && m.IsBot));
        }

        var hours = await rows
            .GroupBy(h => h.Hour)
            .Select(g => new { Hour = g.Key, Messages = g.Sum(h => h.MessageCount) })
            .ToListAsync(cancellationToken);

        var heatmap = EmptyHeatmap();
        foreach (var hour in hours)
        {
            // Every UTC hour starts a Zurich hour; on the fall-back day two of them read 02:00.
            var local = ZurichCalendar.ToLocal(hour.Hour);
            heatmap[MondayFirst(local.DayOfWeek)][local.Hour] += hour.Messages;
        }

        return heatmap;
    }

    private async Task<IReadOnlyList<IReadOnlyList<long>>> VoiceHeatmapAsync(
        ActivityStatsQuery query, DateTimeOffset start, DateTimeOffset end, CancellationToken cancellationToken)
    {
        var forUser = query.UserId is not null;
        var userId = query.UserId ?? Guid.Empty;

        // Raw SQL: one session feeds several hour cells, so neither a continuous aggregate nor LINQ can do the split
        // (#111). Each closed session is clipped to the range, generate_series yields the UTC hours it overlaps (Zurich
        // is always a whole number of hours off UTC, so they are Zurich hours), and LEAST/GREATEST give the overlap
        // with each. The fall-back day's two 02:00 hours both land on 02:00, as in the message heatmap. Bots are left
        // out through guild_members.is_bot (voice_sessions has no flag of its own).
        var cells = await db.Database.SqlQuery<VoiceHeatmapCell>($"""
            SELECT EXTRACT(ISODOW FROM h.hour AT TIME ZONE 'Europe/Zurich')::int - 1 AS "Weekday",
                EXTRACT(HOUR FROM h.hour AT TIME ZONE 'Europe/Zurich')::int AS "Hour",
                SUM(EXTRACT(EPOCH FROM LEAST(s.until, h.hour + INTERVAL '1 hour') - GREATEST(s.since, h.hour)))::bigint AS "Seconds"
            FROM (
                SELECT GREATEST(v.occurred_at, {start}) AS since, LEAST(v.ended_at, {end}) AS until
                FROM voice_sessions v
                WHERE v.guild_id = {query.GuildId}
                    AND v.ended_at IS NOT NULL
                    AND v.occurred_at < {end}
                    AND v.ended_at > {start}
                    AND (NOT {forUser} OR v.user_id = {userId})
                    AND ({query.IncludeBots} OR NOT EXISTS (
                        SELECT 1 FROM guild_members m WHERE m.guild_id = v.guild_id AND m.user_id = v.user_id AND m.is_bot))
            ) s
            CROSS JOIN LATERAL generate_series(date_trunc('hour', s.since, 'UTC'), s.until, INTERVAL '1 hour') AS h(hour)
            WHERE h.hour < s.until
            GROUP BY 1, 2
            """).ToListAsync(cancellationToken);

        var heatmap = EmptyHeatmap();
        foreach (var cell in cells)
        {
            heatmap[cell.Weekday][cell.Hour] = cell.Seconds;
        }

        return heatmap;
    }

    private static long[][] EmptyHeatmap()
    {
        var heatmap = new long[7][];
        for (var weekday = 0; weekday < heatmap.Length; weekday++)
        {
            heatmap[weekday] = new long[24];
        }

        return heatmap;
    }

    private static int MondayFirst(DayOfWeek day) => ((int)day + 6) % 7;

    private sealed record VoiceHeatmapCell(int Weekday, int Hour, long Seconds);

    private static IEnumerable<DateOnly> Dates(DateOnly from, DateOnly to)
    {
        for (var date = from; date <= to; date = date.AddDays(1))
        {
            yield return date;
        }
    }

    private static ActivityCounts Sum(IEnumerable<ActivityCounts> counts) =>
        counts.Aggregate(ActivityCounts.Zero, (sum, c) => new ActivityCounts(
            sum.Messages + c.Messages,
            sum.Characters + c.Characters,
            sum.ReactionsGiven + c.ReactionsGiven,
            sum.ReactionsReceived + c.ReactionsReceived,
            sum.VoiceSeconds + c.VoiceSeconds));

    private static ActivityAverages Average(ActivityCounts totals, int days) =>
        days == 0
            ? ActivityAverages.Zero
            : new ActivityAverages(
                (double)totals.Messages / days,
                (double)totals.Characters / days,
                (double)totals.ReactionsGiven / days,
                (double)totals.ReactionsReceived / days,
                (double)totals.VoiceSeconds / days);
}
