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
}
