using System.Text.RegularExpressions;
using Apollo.Web.Client.Theme;

namespace Apollo.Web.Client.Tests.Theme;

public sealed partial class ChartPaletteTests
{
    [Fact]
    public void ChartPalette_ShouldProvideAtLeastEightDistinctColours()
    {
        var colours = ChartPalette.Series;

        colours.Count.ShouldBeGreaterThanOrEqualTo(8);
        colours.Select(c => c.ToUpperInvariant()).Distinct().Count().ShouldBe(colours.Count);
        colours.ShouldAllBe(c => HexColour().IsMatch(c));
    }

    [Fact]
    public void ChartPalette_SeriesColours_ShouldStandOutFromTheSurface()
    {
        foreach (var colour in ChartPalette.Series)
        {
            WcagContrast.Ratio(colour, ApolloColors.Surface).ShouldBeGreaterThanOrEqualTo(3.0, colour);
        }
    }

    [Fact]
    public void ChartPalette_HeatmapScale_ShouldGetBrighterWithEveryStep()
    {
        var steps = ChartPalette.HeatmapScale;

        steps.Count.ShouldBeGreaterThanOrEqualTo(4);
        steps.Select(WcagContrast.RelativeLuminance).ShouldBeInOrder(SortDirection.Ascending);
    }

    [GeneratedRegex("^#[0-9A-Fa-f]{6}$")]
    private static partial Regex HexColour();
}
