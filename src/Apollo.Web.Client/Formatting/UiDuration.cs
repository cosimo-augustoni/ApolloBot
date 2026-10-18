namespace Apollo.Web.Client.Formatting;

/// <summary>Durations such as voice time, in <see cref="UiCulture"/>.</summary>
public static class UiDuration
{
    private const long SecondsPerHour = 3600;

    /// <summary>
    /// Whole hours and minutes, started minutes cut off: <c>35 min</c> under an hour, otherwise <c>1'234 h 5 min</c>.
    /// </summary>
    public static string HoursAndMinutes(long seconds)
    {
        var hours = seconds / SecondsPerHour;
        var minutes = seconds % SecondsPerHour / 60;
        var minutesText = $"{minutes.ToString("N0", UiCulture.Culture)} min";
        return hours == 0 ? minutesText : $"{hours.ToString("N0", UiCulture.Culture)} h {minutesText}";
    }

    /// <summary>Hours rounded to one decimal, e.g. for a chart axis: 5400 s is <c>1.5</c>.</summary>
    public static double Hours(long seconds) => Math.Round(seconds / (double)SecondsPerHour, 1);
}
