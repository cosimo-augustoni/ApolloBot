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
        TimeZoneInfo.ConvertTime(instant, Zone).ToString("dd.MM.yyyy HH:mm", UiCulture.Culture);
}
