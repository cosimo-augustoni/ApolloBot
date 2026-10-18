namespace Apollo.Stats.Activity;

/// <summary>
/// Zurich calendar days and hours (D3). The aggregates store the UTC instant a Zurich day or hour starts (#34); this
/// turns dates into those instants and back, never <c>::date</c> in SQL.
/// </summary>
internal static class ZurichCalendar
{
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    /// <summary>
    /// Midnight at the start of <paramref name="day"/> in Zurich, as UTC. Midnight always exists there (DST switches at
    /// 02:00/03:00).
    /// </summary>
    public static DateTimeOffset StartOf(DateOnly day) =>
        new(TimeZoneInfo.ConvertTimeToUtc(day.ToDateTime(TimeOnly.MinValue), Zone), TimeSpan.Zero);

    public static DateTime ToLocal(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone).DateTime;

    public static DateOnly DayOf(DateTimeOffset instant) => DateOnly.FromDateTime(ToLocal(instant));
}
