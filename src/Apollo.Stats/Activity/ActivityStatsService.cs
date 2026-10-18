using System.Linq.Expressions;
using Apollo.Data;
using Apollo.Data.Activity;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Stats.Activity;

/// <summary>
/// Reads <c>daily_user_channel_activity</c> and <c>hourly_activity_heatmap</c> (#34) and <c>tracking_periods</c> for a
/// Zurich date range. The Zurich days become the UTC instants of their midnights, which is what the aggregates' buckets
/// hold, so the range is <c>[midnight of From, midnight after To)</c>.
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

        var heatmap = new long[7][];
        for (var weekday = 0; weekday < heatmap.Length; weekday++)
        {
            heatmap[weekday] = new long[24];
        }

        foreach (var hour in hours)
        {
            // Every UTC hour starts a Zurich hour; on the fall-back day two of them read 02:00.
            var local = ZurichCalendar.ToLocal(hour.Hour);
            heatmap[MondayFirst(local.DayOfWeek)][local.Hour] += hour.Messages;
        }

        return heatmap;
    }

    private static int MondayFirst(DayOfWeek day) => ((int)day + 6) % 7;

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
