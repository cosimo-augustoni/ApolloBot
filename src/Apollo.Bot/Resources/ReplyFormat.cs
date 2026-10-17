using System.Globalization;

namespace Apollo.Bot.Resources;

/// <summary>
/// Formats numbers and dates in reply text: <c>de-CH</c>, dates in <c>Europe/Zurich</c> (scope §5.1, D3), whatever the
/// culture and time zone of the host are.
/// </summary>
internal static class ReplyFormat
{
    private static readonly TimeZoneInfo _zurich = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    /// <summary>
    /// <c>de-CH</c> with the ASCII apostrophe as group separator (<c>1'234</c>), like the web UI.
    /// </summary>
    private static readonly CultureInfo _culture = CreateCulture();

    /// <summary>
    /// The date in Zurich, e.g. <c>16.01.2026</c>.
    /// </summary>
    public static string Date(DateTimeOffset at) => TimeZoneInfo.ConvertTime(at, _zurich).ToString("dd.MM.yyyy", _culture);

    public static string Number(long value) => value.ToString("N0", _culture);

    private static CultureInfo CreateCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("de-CH").Clone();
        culture.NumberFormat.NumberGroupSeparator = "'";
        return CultureInfo.ReadOnly(culture);
    }
}
