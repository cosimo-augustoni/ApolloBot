using System.Globalization;

namespace Apollo.Web.Client.Tests.Theme;

/// <summary>WCAG 2.x relative luminance and contrast ratio for <c>#RRGGBB</c> colours.</summary>
internal static class WcagContrast
{
    public static double Ratio(string foreground, string background)
    {
        var a = RelativeLuminance(foreground);
        var b = RelativeLuminance(background);
        return (Math.Max(a, b) + 0.05) / (Math.Min(a, b) + 0.05);
    }

    public static double RelativeLuminance(string hex)
    {
        var r = Channel(hex, 1);
        var g = Channel(hex, 3);
        var b = Channel(hex, 5);
        return (0.2126 * r) + (0.7152 * g) + (0.0722 * b);
    }

    private static double Channel(string hex, int start)
    {
        var value = int.Parse(hex.AsSpan(start, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255.0;
        return value <= 0.04045 ? value / 12.92 : Math.Pow((value + 0.055) / 1.055, 2.4);
    }
}
