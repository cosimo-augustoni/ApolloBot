using Microsoft.Extensions.Localization;

namespace Apollo.Web.Client.Formatting;

public static class LocalizerExtensions
{
    /// <summary>
    /// The text for <paramref name="count"/> things: <paramref name="oneKey"/> for exactly one, otherwise
    /// <paramref name="otherKey"/> with the count formatted in <see cref="UiCulture"/> (<c>1'234</c>) as <c>{0}</c>.
    /// </summary>
    public static string Count(this IStringLocalizer localizer, int count, string oneKey, string otherKey)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        return count == 1 ? localizer[oneKey] : localizer[otherKey, count.ToString("N0", UiCulture.Culture)];
    }

    /// <summary>
    /// How long before <paramref name="now"/> <paramref name="instant"/> was, in the largest whole unit: <c>vor 12 s</c>,
    /// <c>vor 28 min</c>, <c>vor 3 h</c>, <c>vor 2 Tagen</c>. An instant after <paramref name="now"/> counts as 0 s.
    /// </summary>
    public static string Ago(this IStringLocalizer localizer, DateTimeOffset instant, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(localizer);
        var elapsed = now > instant ? now - instant : TimeSpan.Zero;
        return elapsed switch
        {
            { TotalMinutes: < 1 } => localizer["Time_SecondsAgo", Whole(elapsed.TotalSeconds)],
            { TotalHours: < 1 } => localizer["Time_MinutesAgo", Whole(elapsed.TotalMinutes)],
            { TotalDays: < 1 } => localizer["Time_HoursAgo", Whole(elapsed.TotalHours)],
            { TotalDays: < 2 } => localizer["Time_OneDayAgo"],
            _ => localizer["Time_DaysAgo", Whole(elapsed.TotalDays)],
        };
    }

    private static string Whole(double value) => Math.Floor(value).ToString("N0", UiCulture.Culture);
}
