using Apollo.Data;
using Apollo.Data.Tracking;
using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Web.Client.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Apollo.Web.Admin;

/// <summary>
/// The admin bot status page (#31, #91, scope §7.2) for the configured guild (the guild of the identity providers,
/// scope §4.3). From the heartbeats the bot writes to <c>bot_status</c> (#24): the latest one and whether it is older
/// than <see cref="BotStatusOptions.StaleAfter"/>, the latest one that said connected, and the mean latency of the last
/// <see cref="LatencyWindow"/>. Bot and web are separate processes, so the shared DB is the only link. From the
/// recorded data: the open voice sessions, the newest row, the events per hour over the last <see cref="Hours"/>
/// <c>Europe/Zurich</c> hours (D3) per table, and whether a tracking role is set (#62).
/// </summary>
public sealed class BotStatusQuery(
    ApolloDbContext db,
    IEnumerable<IPlatformIdentityProvider> providers,
    RoleSettingsQuery settings,
    IOptions<BotStatusOptions> options,
    TimeProvider time)
{
    /// <summary>The hours in <see cref="BotStatusDto.EventsPerHour"/>, the current one included.</summary>
    public const int Hours = 24;

    /// <summary>The heartbeats <see cref="BotStatusDto.GatewayLatencyMeanMs"/> averages: those of the last 5 minutes.</summary>
    public static readonly TimeSpan LatencyWindow = TimeSpan.FromMinutes(5);

    // The tables counted per hour, in DataEventType order; the SQL names each with its key.
    private static readonly (string Key, DataEventType Type)[] _tables =
    [
        ("message", DataEventType.Message),
        ("voice_session", DataEventType.VoiceSession),
        ("reaction", DataEventType.Reaction),
        ("membership", DataEventType.Membership),
        ("presence", DataEventType.PresenceActivity),
    ];

    public async Task<BotStatusDto> GetAsync(CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var guildIds = await ConfiguredGuilds.IdsAsync(db, providers, cancellationToken);
        var eventsPerHour = await CountEventsPerHourAsync(guildIds, now, cancellationToken);
        var lastRecordedEvent = await FindLastRecordedEventAsync(guildIds, cancellationToken);
        var openVoiceSessions = await ListOpenVoiceSessionsAsync(guildIds, cancellationToken);
        var noTrackingRole = await HasNoTrackingRoleAsync(cancellationToken);

        var heartbeats = db.BotStatuses.AsNoTracking().Where(s => guildIds.Contains(s.GuildId));
        var heartbeat = await heartbeats.OrderByDescending(s => s.OccurredAt).FirstOrDefaultAsync(cancellationToken);
        if (heartbeat is null)
        {
            return new BotStatusDto(now, null, false, null, null, lastRecordedEvent, openVoiceSessions, false, noTrackingRole, eventsPerHour);
        }

        var lastConnectedAt = await heartbeats.Where(s => s.Connected)
            .OrderByDescending(s => s.OccurredAt)
            .Select(s => (DateTimeOffset?)s.OccurredAt)
            .FirstOrDefaultAsync(cancellationToken);
        var latencyFrom = now - LatencyWindow;
        var latencyMean = await heartbeats.Where(s => s.OccurredAt >= latencyFrom && s.OccurredAt <= now)
            .AverageAsync(s => (double?)s.GatewayLatencyMs, cancellationToken);

        return new BotStatusDto(
            now,
            heartbeat.OccurredAt,
            heartbeat.Connected,
            lastConnectedAt,
            latencyMean is { } mean ? (int)Math.Round(mean, MidpointRounding.AwayFromZero) : null,
            lastRecordedEvent,
            openVoiceSessions,
            now - heartbeat.OccurredAt > options.Value.StaleAfter,
            noTrackingRole,
            eventsPerHour);
    }

    private async Task<List<HourlyEventCountDto>> CountEventsPerHourAsync(Guid[] guilds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        // Zurich is a whole number of hours off UTC (+1, +2, switching at 01:00 UTC), so its hours start on UTC hours.
        var nowUtc = now.UtcDateTime;
        var currentHour = new DateTimeOffset(nowUtc.Date.AddHours(nowUtc.Hour), TimeSpan.Zero);
        var from = currentHour.AddHours(-(Hours - 1));
        var to = currentHour.AddHours(1);

        // Raw SQL: EF has no translation for time_bucket with a time zone, nor for one count over five hypertables.
        // voice_sessions and presence_activity_sessions count by their start (occurred_at, #16).
        var rows = await db.Database.SqlQuery<HourCount>($"""
            SELECT e.table_key AS "TableKey", time_bucket('1 hour', e.occurred_at, 'Europe/Zurich') AS "HourStart", count(*)::int AS "Count"
            FROM (
                SELECT 'message' AS table_key, occurred_at FROM message_events WHERE guild_id = ANY({guilds}) AND occurred_at >= {from} AND occurred_at < {to}
                UNION ALL
                SELECT 'reaction', occurred_at FROM reaction_events WHERE guild_id = ANY({guilds}) AND occurred_at >= {from} AND occurred_at < {to}
                UNION ALL
                SELECT 'membership', occurred_at FROM membership_events WHERE guild_id = ANY({guilds}) AND occurred_at >= {from} AND occurred_at < {to}
                UNION ALL
                SELECT 'voice_session', occurred_at FROM voice_sessions WHERE guild_id = ANY({guilds}) AND occurred_at >= {from} AND occurred_at < {to}
                UNION ALL
                SELECT 'presence', occurred_at FROM presence_activity_sessions WHERE guild_id = ANY({guilds}) AND occurred_at >= {from} AND occurred_at < {to}
            ) e
            GROUP BY 1, 2
            """).ToListAsync(cancellationToken);
        var counts = rows.ToDictionary(r => (r.TableKey, r.HourStart), r => r.Count);

        return [.. Enumerable.Range(0, Hours)
            .Select(i => from.AddHours(i))
            .Select(hour =>
            {
                List<EventTypeCountDto> types = [.. _tables.Select(t => new EventTypeCountDto(t.Type, counts.GetValueOrDefault((t.Key, hour))))];
                return new HourlyEventCountDto(hour, types.Sum(t => t.Count), types);
            })];
    }

    /// <summary>
    /// The newest row of the counted tables, each read newest first on its own (an index scan per hypertable); on a tie
    /// the first table in <see cref="DataEventType"/> order wins.
    /// </summary>
    private async Task<LastRecordedEventDto?> FindLastRecordedEventAsync(Guid[] guilds, CancellationToken cancellationToken)
    {
        var newest = new List<(DataEventType Type, NewestRow Row)>();
        await AddNewestAsync(DataEventType.Message, db.MessageEvents.AsNoTracking().Where(e => guilds.Contains(e.GuildId))
            .OrderByDescending(e => e.OccurredAt).Select(e => new NewestRow(e.OccurredAt, e.ChannelId)));
        await AddNewestAsync(DataEventType.VoiceSession, db.VoiceSessions.AsNoTracking().Where(e => guilds.Contains(e.GuildId))
            .OrderByDescending(e => e.OccurredAt).Select(e => new NewestRow(e.OccurredAt, e.ChannelId)));
        await AddNewestAsync(DataEventType.Reaction, db.ReactionEvents.AsNoTracking().Where(e => guilds.Contains(e.GuildId))
            .OrderByDescending(e => e.OccurredAt).Select(e => new NewestRow(e.OccurredAt, e.ChannelId)));
        await AddNewestAsync(DataEventType.Membership, db.MembershipEvents.AsNoTracking().Where(e => guilds.Contains(e.GuildId))
            .OrderByDescending(e => e.OccurredAt).Select(e => new NewestRow(e.OccurredAt, null)));
        await AddNewestAsync(DataEventType.PresenceActivity, db.PresenceActivitySessions.AsNoTracking().Where(e => guilds.Contains(e.GuildId))
            .OrderByDescending(e => e.OccurredAt).Select(e => new NewestRow(e.OccurredAt, null)));
        if (newest.Count == 0)
        {
            return null;
        }

        var (type, last) = newest.MaxBy(n => n.Row.OccurredAt);
        var channelName = last.ChannelId is { } channelId
            ? await db.Channels.AsNoTracking().Where(c => c.Id == channelId).Select(c => c.Name).FirstOrDefaultAsync(cancellationToken)
            : null;
        return new LastRecordedEventDto(type, last.OccurredAt, channelName);

        async Task AddNewestAsync(DataEventType table, IQueryable<NewestRow> rows)
        {
            if (await rows.FirstOrDefaultAsync(cancellationToken) is { } row)
            {
                newest.Add((table, row));
            }
        }
    }

    /// <summary>The voice sessions without an end (#16); only tracked members have rows, so no gate check is needed.</summary>
    private async Task<List<OpenVoiceSessionDto>> ListOpenVoiceSessionsAsync(Guid[] guilds, CancellationToken cancellationToken) =>
        await (
                from session in db.VoiceSessions.AsNoTracking()
                where guilds.Contains(session.GuildId) && session.EndedAt == null
                join user in db.Users on session.UserId equals user.Id
                join channel in db.Channels on session.ChannelId equals channel.Id
                orderby session.OccurredAt, user.DisplayName
                select new OpenVoiceSessionDto(user.DisplayName, channel.Id, channel.Name, session.OccurredAt))
            .ToListAsync(cancellationToken);

    /// <summary>Whether a platform whose configured guild the guild state sync has seen has no tracking role (#62).</summary>
    private async Task<bool> HasNoTrackingRoleAsync(CancellationToken cancellationToken)
    {
        foreach (var provider in providers.DistinctBy(p => p.Platform))
        {
            if (await settings.FindSyncedGuildAsync(provider.Guild, cancellationToken) is { } guildId
                && (await settings.GetMemberTrackingAsync(guildId, provider.Platform, cancellationToken)).Mode == TrackingMode.NoTrackingRole)
            {
                return true;
            }
        }

        return false;
    }

    private sealed record NewestRow(DateTimeOffset OccurredAt, Guid? ChannelId);

    private sealed class HourCount
    {
        public required string TableKey { get; init; }

        public DateTimeOffset HourStart { get; init; }

        public int Count { get; init; }
    }
}
