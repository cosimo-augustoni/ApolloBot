namespace Apollo.Web.Admin;

/// <summary>Calendar days in <c>Europe/Zurich</c>, the zone all bucketing uses (D3).</summary>
public static class ZurichTime
{
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    /// <summary>The Zurich calendar day <paramref name="instant"/> falls on.</summary>
    public static DateOnly DayOf(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    /// <summary>
    /// The time between two instants on Zurich clocks: from 25.10.2025 00:00 to 26.10.2026 00:00 is 366 days, although
    /// the end of summer time makes it 366 days and an hour of real time.
    /// </summary>
    public static TimeSpan WallClockSpan(DateTimeOffset start, DateTimeOffset end) =>
        TimeZoneInfo.ConvertTime(end, Zone).DateTime - TimeZoneInfo.ConvertTime(start, Zone).DateTime;

    /// <summary>Midnight at the start of <paramref name="day"/> in Zurich, as UTC. Midnight always exists there (DST switches at 02:00/03:00).</summary>
    public static DateTimeOffset StartOf(DateOnly day) =>
        new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Zone), TimeSpan.Zero);
}
