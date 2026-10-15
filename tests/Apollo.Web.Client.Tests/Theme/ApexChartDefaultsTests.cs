using ApexCharts;
using Apollo.Web.Client.Theme;

namespace Apollo.Web.Client.Tests.Theme;

public sealed class ApexChartDefaultsTests
{
    private sealed record Point(string Day, int Count);

    [Fact]
    public void ApexChartDefaults_ShouldApplyPaletteAndDarkMode()
    {
        var options = ApexChartDefaults.Create<Point>();

        options.Colors.ShouldBe(ChartPalette.Series);
        options.Theme.Mode.ShouldBe(Mode.Dark);
        options.Tooltip.Theme.ShouldBe(Mode.Dark);
        options.Chart.Background.ShouldBe(ChartPalette.Background);
        options.Chart.FontFamily.ShouldBe(ApolloTheme.FontFamilyCss);
    }

    [Fact]
    public void ApexChartDefaults_ShouldApplyGridAndLabelColours()
    {
        var options = ApexChartDefaults.Create<Point>();

        options.Grid.BorderColor.ShouldBe(ChartPalette.Grid);
        options.Chart.ForeColor.ShouldBe(ChartPalette.Label);
        ((List<string>)options.Xaxis.Labels.Style.Colors).ShouldBe([ChartPalette.Label]);
        options.Xaxis.AxisBorder.Color.ShouldBe(ChartPalette.Axis);
        options.Yaxis.ShouldHaveSingleItem();
        ((List<string>)options.Yaxis[0].Labels.Style.Colors).ShouldBe([ChartPalette.Label]);
        ((List<string>)options.Legend.Labels.Colors).ShouldBe([ChartPalette.Label]);
    }

    [Fact]
    public void ApexChartDefaults_ShouldFormatValueAxisAndTooltipWithSwissGrouping()
    {
        var options = ApexChartDefaults.Create<Point>();

        options.Yaxis[0].Labels.Formatter.ShouldBe(ApexChartDefaults.SwissNumberFormatter);
        options.Tooltip.Y.Formatter.ShouldBe(ApexChartDefaults.SwissNumberFormatter);
        options.Xaxis.Labels.Formatter.ShouldBeNull();
        ApexChartDefaults.SwissNumberFormatter.ShouldContain("de-CH");
    }

    [Fact]
    public void ApexChartDefaults_ShouldReturnIndependentInstances()
    {
        var first = ApexChartDefaults.Create<Point>();
        var second = ApexChartDefaults.Create<Point>();

        first.Colors.Add("#000000");

        second.Colors.ShouldBe(ChartPalette.Series);
    }

    [Fact]
    public void ApexChartDefaults_Heatmap_ShouldUseHeatmapScaleAsColourRanges()
    {
        var options = ApexChartDefaults.CreateHeatmap<Point>(maxValue: 100);

        var ranges = options.PlotOptions.Heatmap.ColorScale.Ranges;
        ranges.Select(r => r.Color).ShouldBe(ChartPalette.HeatmapScale);
        ranges[0].From.ShouldBe(0);
        ranges[^1].To.ShouldBe(100);
        options.DataLabels.Enabled.ShouldBeFalse();
    }
}
