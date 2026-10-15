using ApexCharts;

namespace Apollo.Web.Client.Theme;

/// <summary>
/// Builds ApexCharts options with the <see cref="ChartPalette"/> and dark styling applied.
/// Every call returns a new instance, so callers may adjust the result freely.
/// </summary>
public static class ApexChartDefaults
{
    /// <summary>
    /// JS formatter for numeric labels: <c>de-CH</c> grouping with the ASCII apostrophe, like <see cref="Formatting.UiCulture"/>.
    /// Charts render in the browser, so .NET formatting cannot be used. Non-numbers (categories) pass through unchanged.
    /// </summary>
    public const string SwissNumberFormatter =
        """function (value) { return typeof value === 'number' ? value.toLocaleString('de-CH', { maximumFractionDigits: 1 }).replace(/’/g, "'") : value; }""";

    public static ApexChartOptions<T> Create<T>() where T : class => new()
    {
        Colors = [.. ChartPalette.Series],
        Theme = new ApexCharts.Theme { Mode = Mode.Dark },
        Chart = new Chart
        {
            Background = ChartPalette.Background,
            ForeColor = ChartPalette.Label,
            FontFamily = ApolloTheme.FontFamilyCss,
            Toolbar = new Toolbar { Show = false },
            Zoom = new Zoom { Enabled = false },
        },
        Grid = new Grid
        {
            BorderColor = ChartPalette.Grid,
            StrokeDashArray = 4,
        },
        Xaxis = new XAxis
        {
            Labels = new XAxisLabels { Style = LabelStyle() },
            AxisBorder = new AxisBorder { Color = ChartPalette.Axis },
            AxisTicks = new AxisTicks { Color = ChartPalette.Axis },
        },
        Yaxis = [new YAxis { Labels = new YAxisLabels { Style = LabelStyle(), Formatter = SwissNumberFormatter } }],
        Legend = new Legend
        {
            Labels = new LegendLabels { Colors = new List<string> { ChartPalette.Label } },
        },
        Tooltip = new Tooltip { Theme = Mode.Dark, Y = new TooltipY { Formatter = SwissNumberFormatter } },
        DataLabels = new DataLabels { Enabled = false },
    };

    /// <summary>Heatmap options: cells coloured by <see cref="ChartPalette.HeatmapScale"/> in equal steps from 0 to <paramref name="maxValue"/>.</summary>
    public static ApexChartOptions<T> CreateHeatmap<T>(double maxValue) where T : class
    {
        var options = Create<T>();
        var steps = ChartPalette.HeatmapScale;
        var width = maxValue / steps.Count;

        options.PlotOptions = new PlotOptions
        {
            Heatmap = new PlotOptionsHeatmap
            {
                Radius = 3,
                EnableShades = false,
                ColorScale = new PlotOptionsHeatmapColorScale
                {
                    Ranges =
                    [
                        .. steps.Select((colour, i) => new PlotOptionsHeatmapColorScaleRange
                        {
                            From = i * width,
                            To = i == steps.Count - 1 ? maxValue : (i + 1) * width,
                            Color = colour,
                        }),
                    ],
                },
            },
        };
        options.Stroke = new Stroke { Width = 2, Colors = [ChartPalette.Background] };
        options.Legend.Show = false;
        return options;
    }

    private static AxisLabelStyle LabelStyle() => new() { Colors = new List<string> { ChartPalette.Label } };
}
