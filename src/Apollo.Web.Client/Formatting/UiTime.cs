namespace Apollo.Web.Client.Formatting;

/// <summary>
/// Shows instants as <c>Europe/Zurich</c> local time (scope D3), independent of the browser's time zone. The API
/// sends UTC.
/// </summary>
public static class UiTime
{
    public static TimeZoneInfo Zone { get; } = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    /// <summary>Date and time as <c>dd.MM.yyyy HH:mm</c>, e.g. <c>15.10.2026 10:30</c>.</summary>
    public static string FormatDateTime(DateTimeOffset instant) =>
        ToZurich(instant).ToString("dd.MM.yyyy HH:mm", UiCulture.Culture);

    /// <summary>Time of day with seconds, e.g. <c>14:30:12</c>.</summary>
    public static string FormatTimeWithSeconds(DateTimeOffset instant) =>
        ToZurich(instant).ToString("HH:mm:ss", UiCulture.Culture);

    /// <summary>
    /// When something last happened, seen from <paramref name="now"/>: <c>14:02</c> on the same day in Zurich,
    /// otherwise with the date (<c>14.01.2026 23:59</c>).
    /// </summary>
    public static string FormatSince(DateTimeOffset instant, DateTimeOffset now)
    {
        var local = ToZurich(instant);
        return local.Date == ToZurich(now).Date
            ? local.ToString("HH:mm", UiCulture.Culture)
            : FormatDateTime(instant);
    }

    private static DateTimeOffset ToZurich(DateTimeOffset instant) => TimeZoneInfo.ConvertTime(instant, Zone);
}
