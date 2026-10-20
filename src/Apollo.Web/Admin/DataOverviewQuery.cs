using Apollo.Data;
using Apollo.Data.Tracking;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Web.Client.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Web.Admin;

/// <summary>
/// The overview of the admin data page (#32, scope §7.2) for the configured guild (scope §4.3): the rows of each raw
/// event table per <c>Europe/Zurich</c> day (D3), and the tracked and untracked members.
/// </summary>
public sealed class DataOverviewQuery(
    ApolloDbContext db,
    IEnumerable<IPlatformIdentityProvider> providers,
    RoleSettingsQuery settings,
    TimeProvider time)
{
    /// <summary>The days counted when the caller names no range, today included.</summary>
    public const int DefaultDays = 30;

    /// <summary>The longest range a caller may ask for, so every query stays bounded in time (chunk exclusion).</summary>
    public const int MaxDays = 366;

    // The SQL names each table with one of these keys, in DataEventType order.
    private static readonly Dictionary<string, DataEventType> _tableKeys = new()
    {
        ["message"] = DataEventType.Message,
        ["voice_session"] = DataEventType.VoiceSession,
        ["voice_segment"] = DataEventType.VoiceStateSegment,
        ["reaction"] = DataEventType.Reaction,
        ["membership"] = DataEventType.Membership,
        ["presence"] = DataEventType.PresenceActivity,
        ["presence_segment"] = DataEventType.PresenceActivitySegment,
        ["member_name"] = DataEventType.MemberName,
    };

    /// <summary>
    /// The range to count: <paramref name="to"/> defaults to today in Zurich, <paramref name="from"/> to the
    /// <see cref="DefaultDays"/> days ending there.
    /// </summary>
    public (DateOnly From, DateOnly To) ResolveCountsRange(DateOnly? from, DateOnly? to)
    {
        var last = to ?? ZurichTime.DayOf(time.GetUtcNow());
        return (from ?? last.AddDays(-(DefaultDays - 1)), last);
    }

    /// <summary>The rows of each raw event table per Zurich day from <paramref name="from"/> to <paramref name="to"/>, both included.</summary>
    public async Task<EventCountsDto> GetCountsAsync(DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        var start = ZurichTime.StartOf(from);
        var end = ZurichTime.StartOf(to.AddDays(1));
        var guilds = await ConfiguredGuilds.IdsAsync(db, providers, cancellationToken);

        // Raw SQL: EF has no translation for time_bucket with a time zone, nor for one count over eight hypertables.
        // Session and segment tables count by their start (occurred_at, #16, #19, #115).
        var rows = await db.Database.SqlQuery<DayCount>($"""
            SELECT e.table_key AS "TableKey", time_bucket('1 day', e.occurred_at, 'Europe/Zurich') AS "DayStart", count(*)::int AS "Count"
            FROM (
                SELECT 'message' AS table_key, occurred_at FROM message_events WHERE guild_id = ANY({guilds}) AND occurred_at >= {start} AND occurred_at < {end}
                UNION ALL
                SELECT 'voice_session', occurred_at FROM voice_sessions WHERE guild_id = ANY({guilds}) AND occurred_at >= {start} AND occurred_at < {end}
                UNION ALL
                SELECT 'voice_segment', occurred_at FROM voice_state_segments WHERE guild_id = ANY({guilds}) AND occurred_at >= {start} AND occurred_at < {end}
                UNION ALL
                SELECT 'reaction', occurred_at FROM reaction_events WHERE guild_id = ANY({guilds}) AND occurred_at >= {start} AND occurred_at < {end}
                UNION ALL
                SELECT 'membership', occurred_at FROM membership_events WHERE guild_id = ANY({guilds}) AND occurred_at >= {start} AND occurred_at < {end}
                UNION ALL
                SELECT 'presence', occurred_at FROM presence_activity_sessions WHERE guild_id = ANY({guilds}) AND occurred_at >= {start} AND occurred_at < {end}
                UNION ALL
                SELECT 'presence_segment', occurred_at FROM presence_activity_segments WHERE guild_id = ANY({guilds}) AND occurred_at >= {start} AND occurred_at < {end}
                UNION ALL
                SELECT 'member_name', occurred_at FROM member_name_changes WHERE guild_id = ANY({guilds}) AND occurred_at >= {start} AND occurred_at < {end}
            ) e
            GROUP BY 1, 2
            """).ToListAsync(cancellationToken);

        var counts = rows.ToDictionary(r => (_tableKeys[r.TableKey], ZurichTime.DayOf(r.DayStart)), r => r.Count);
        var days = Enumerable.Range(0, to.DayNumber - from.DayNumber + 1).Select(from.AddDays).ToList();

        return new EventCountsDto(from, to, [.. Enum.GetValues<DataEventType>().Select(type =>
        {
            var perDay = days.Select(day => new DailyEventCountDto(day, counts.GetValueOrDefault((type, day)))).ToList();
            return new EventTableCountDto(type, perDay.Sum(d => d.Count), perDay);
        })]);
    }

    /// <summary>
    /// Tracked and untracked members per platform whose configured guild the guild state sync has seen, counted with the
    /// gate's rule by <see cref="RoleSettingsQuery.GetMemberTrackingAsync"/>.
    /// </summary>
    public async Task<IReadOnlyList<MemberTrackingDto>> GetMemberTrackingAsync(CancellationToken cancellationToken)
    {
        var result = new List<MemberTrackingDto>();
        foreach (var provider in providers.DistinctBy(p => p.Platform))
        {
            if (await settings.FindSyncedGuildAsync(provider.Guild, cancellationToken) is not { } guildId)
            {
                continue;
            }

            var tracking = await settings.GetMemberTrackingAsync(guildId, provider.Platform, cancellationToken);
            result.Add(new MemberTrackingDto(
                provider.Platform.ToString(), tracking.Tracked, tracking.Untracked, tracking.Tracked + tracking.Untracked, tracking.Mode.ToDto()));
        }

        return result;
    }

    private sealed class DayCount
    {
        public required string TableKey { get; init; }

        public DateTimeOffset DayStart { get; init; }

        public int Count { get; init; }
    }
}
