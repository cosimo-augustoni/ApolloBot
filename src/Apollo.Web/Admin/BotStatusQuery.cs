using Apollo.Data;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Web.Client.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Apollo.Web.Admin;

/// <summary>
/// The admin bot status page (#31, scope §7.2): the latest heartbeat the bot wrote to <c>bot_status</c> (#24) for the
/// configured guild (the guild of the identity providers, scope §4.3), and whether it is older than
/// <see cref="BotStatusOptions.StaleAfter"/>. Bot and web are separate processes, so the shared DB is the only link.
/// Also the events recorded per hour over the last <see cref="Hours"/> <c>Europe/Zurich</c> hours (D3).
/// </summary>
public sealed class BotStatusQuery(
    ApolloDbContext db,
    IEnumerable<IPlatformIdentityProvider> providers,
    IOptions<BotStatusOptions> options,
    TimeProvider time)
{
    /// <summary>The hours in <see cref="BotStatusDto.EventsPerHour"/>, the current one included.</summary>
    public const int Hours = 24;

    public async Task<BotStatusDto> GetAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var guildIds = await ConfiguredGuildIdsAsync(cancellationToken);
        var eventsPerHour = await CountEventsPerHourAsync(guildIds, now, cancellationToken);

        var heartbeat = await db.BotStatuses.AsNoTracking()
            .Where(s => guildIds.Contains(s.GuildId))
            .OrderByDescending(s => s.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);

        if (heartbeat is null)
        {
            return new BotStatusDto(now, null, false, null, null, 0, false, eventsPerHour);
        }

        return new BotStatusDto(
            now,
            heartbeat.OccurredAt,
            heartbeat.Connected,
            heartbeat.GatewayLatencyMs,
            heartbeat.LastEventAt,
            heartbeat.OpenVoiceSessions,
            now - heartbeat.OccurredAt > options.Value.StaleAfter,
            eventsPerHour);
    }

    private async Task<List<HourlyEventCountDto>> CountEventsPerHourAsync(List<Guid> guildIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Zurich is a whole number of hours off UTC (+1, +2, switching at 01:00 UTC), so its hours start on UTC hours.
        var nowUtc = now.UtcDateTime;
        var currentHour = new DateTimeOffset(nowUtc.Date.AddHours(nowUtc.Hour), TimeSpan.Zero);
        var from = currentHour.AddHours(-(Hours - 1));
        var to = currentHour.AddHours(1);
        var guilds = guildIds.ToArray();

        // Raw SQL: EF has no translation for time_bucket with a time zone, nor for one count over five hypertables.
        // voice_sessions and presence_activity_sessions count by their start (occurred_at, #16).
        var counts = await db.Database.SqlQuery<HourCount>($"""
            SELECT time_bucket('1 hour', e.occurred_at, 'Europe/Zurich') AS "HourStart", count(*)::int AS "Count"
            FROM (
                SELECT occurred_at FROM message_events WHERE guild_id = ANY({guilds}) AND occurred_at >= {from} AND occurred_at < {to}
                UNION ALL
                SELECT occurred_at FROM reaction_events WHERE guild_id = ANY({guilds}) AND occurred_at >= {from} AND occurred_at < {to}
                UNION ALL
                SELECT occurred_at FROM membership_events WHERE guild_id = ANY({guilds}) AND occurred_at >= {from} AND occurred_at < {to}
                UNION ALL
                SELECT occurred_at FROM voice_sessions WHERE guild_id = ANY({guilds}) AND occurred_at >= {from} AND occurred_at < {to}
                UNION ALL
                SELECT occurred_at FROM presence_activity_sessions WHERE guild_id = ANY({guilds}) AND occurred_at >= {from} AND occurred_at < {to}
            ) e
            GROUP BY 1
            """).ToDictionaryAsync(c => c.HourStart, c => c.Count, cancellationToken);

        return [.. Enumerable.Range(0, Hours)
            .Select(i => from.AddHours(i))
            .Select(hour => new HourlyEventCountDto(hour, counts.GetValueOrDefault(hour)))];
    }

    private async Task<List<Guid>> ConfiguredGuildIdsAsync(CancellationToken cancellationToken)
    {
        var guildIds = new List<Guid>();
        foreach (var guild in providers.Select(p => p.Guild).Distinct())
        {
            guildIds.AddRange(await db.GuildPlatformIds.AsNoTracking()
                .Where(m => m.Platform == guild.Platform && m.ExternalId == guild.ExternalId)
                .Select(m => m.GuildId)
                .ToListAsync(cancellationToken));
        }

        return guildIds;
    }

    private sealed class HourCount
    {
        public DateTimeOffset HourStart { get; init; }

        public int Count { get; init; }
    }
}
