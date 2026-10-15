using System.Globalization;

namespace Apollo.Web.Client.Formatting;

/// <summary>
/// The culture used to format numbers and dates in the UI: <c>de-CH</c>, independent of the browser language.
/// </summary>
public static class UiCulture
{
    /// <summary>Shown instead of a value that is missing.</summary>
    public const string Placeholder = "–";

    /// <summary>
    /// <c>de-CH</c> with the ASCII apostrophe as group separator (<c>1'234'567</c>). Current CLDR data uses
    /// U+2019 instead, and older ICU builds differ again, so the separator is pinned to stay the same everywhere.
    /// </summary>
    public static CultureInfo Culture { get; } = CreateCulture();

    private static CultureInfo CreateCulture()
    {
        var culture = (CultureInfo)CultureInfo.GetCultureInfo("de-CH").Clone();
        culture.NumberFormat.NumberGroupSeparator = "'";
        culture.NumberFormat.CurrencyGroupSeparator = "'";
        culture.NumberFormat.PercentGroupSeparator = "'";
        culture.NumberFormat.NumberDecimalSeparator = ".";
        culture.NumberFormat.NegativeSign = "-";
        return CultureInfo.ReadOnly(culture);
    }
}
