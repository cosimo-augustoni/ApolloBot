using Apollo.Stats.Activity;

namespace Apollo.Stats.Sanity;

/// <summary>
/// What <see cref="IDataSanityService"/> checks: a guild over the Zurich calendar days <see cref="From"/> to
/// <see cref="To"/>, both inclusive, as the other stats queries (#35, #36).
/// </summary>
/// <param name="GuildId">The internal guild ID.</param>
/// <param name="From">The first Zurich day of the range.</param>
/// <param name="To">The last Zurich day of the range; must not be before <paramref name="From"/>.</param>
/// <param name="HeartbeatGapThreshold">
/// How far apart two consecutive heartbeats may be before they count as a gap. The host passes the threshold of its
/// stale warning (#31), so both agree; by default <see cref="DefaultHeartbeatGapThreshold"/>.
/// </param>
public sealed record DataSanityQuery(Guid GuildId, DateOnly From, DateOnly To, TimeSpan HeartbeatGapThreshold)
{
    /// <summary>How many Zurich days the default range covers, today included.</summary>
    public const int DefaultDays = 30;

    /// <summary>How many example rows a check returns at most.</summary>
    public const int MaxExamples = 20;

    /// <summary>The bot writes a heartbeat every 30 s (#24); four missed ones are a gap.</summary>
    public static readonly TimeSpan DefaultHeartbeatGapThreshold = TimeSpan.FromMinutes(2);

    /// <summary>A session longer than this, or open for longer than this, is reported.</summary>
    public static readonly TimeSpan LongSessionThreshold = TimeSpan.FromHours(24);

    public DataSanityQuery(Guid guildId, DateOnly from, DateOnly to)
        : this(guildId, from, to, DefaultHeartbeatGapThreshold)
    {
    }

    /// <summary>The last <see cref="DefaultDays"/> Zurich days up to today, both included.</summary>
    public static (DateOnly From, DateOnly To) DefaultRange(TimeProvider time)
    {
        ArgumentNullException.ThrowIfNull(time);
        var today = ZurichCalendar.DayOf(time.GetUtcNow());
        return (today.AddDays(1 - DefaultDays), today);
    }
}
