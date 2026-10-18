using ApexCharts;
using Apollo.Web.Client.Components.Stats;
using Apollo.Web.Client.Theme;
using Bunit;
using MudBlazor;

namespace Apollo.Web.Client.Tests.Components.Stats;

/// <summary>
/// The weekday × hour heatmap (#37): 24 hours on wide screens, 8 blocks of 3 hours below 600px; voice time by default,
/// messages on request (#111).
/// </summary>
public sealed class ActivityHeatmapTests : ApolloTestContext
{
    private static readonly IReadOnlyList<IReadOnlyList<long>> _messages =
        [.. Enumerable.Range(0, 7).Select(day => (IReadOnlyList<long>)[.. Enumerable.Range(0, 24).Select(hour => (long)(day + hour))])];

    private static readonly IReadOnlyList<IReadOnlyList<long>> _voice =
        [.. Enumerable.Range(0, 7).Select(day => (IReadOnlyList<long>)[.. Enumerable.Range(0, 24).Select(hour => 60L * (100 + (day * 24) + hour))])];

    [Fact]
    public void ActivityHeatmap_OnAWideScreen_ShouldShowEveryHourWithMondayOnTop()
    {
        var heatmap = RenderHeatmap();

        var series = heatmap.FindComponents<ApexPointSeries<HeatmapBlock>>();
        // ApexCharts draws the first series at the bottom.
        series.Select(s => s.Instance.Name).ShouldBe(["So", "Sa", "Fr", "Do", "Mi", "Di", "Mo"]);
        series.ShouldAllBe(s => s.Instance.Items.Count() == 24);
        series[^1].Instance.XValue(new HeatmapBlock(9, 0)).ShouldBe("09");
    }

    [Fact]
    public void ActivityHeatmap_ByDefault_ShouldShowVoiceTimeInHoursAndMinutes()
    {
        var heatmap = RenderHeatmap();

        var series = heatmap.FindComponents<ApexPointSeries<HeatmapBlock>>();
        series[^1].Instance.Items.First().ShouldBe(new HeatmapBlock(0, 6000));
        Caption(heatmap).ShouldBe("Voice-Zeit pro Stunde");
        heatmap.FindComponent<ApexChart<HeatmapBlock>>().Instance.Options.Tooltip.Y.Formatter.ShouldBe(ApexChartDefaults.DurationFormatter);
        SelectedToggle(heatmap).ShouldBe("Voice-Stunden");
    }

    [Fact]
    public void ActivityHeatmap_WhenMessagesChosen_ShouldShowMessages()
    {
        var heatmap = RenderHeatmap();

        heatmap.FindAll(".mud-toggle-item").Single(b => b.TextContent.Trim() == "Nachrichten").Click();

        heatmap.WaitForAssertion(() => Caption(heatmap).ShouldBe("Nachrichten pro Stunde"));
        var series = heatmap.FindComponents<ApexPointSeries<HeatmapBlock>>();
        series[^1].Instance.Items.First().ShouldBe(new HeatmapBlock(0, 0));
        heatmap.FindComponent<ApexChart<HeatmapBlock>>().Instance.Options.Tooltip.Y.Formatter.ShouldBe(ApexChartDefaults.SwissNumberFormatter);
    }

    [Fact]
    public void ActivityHeatmap_WhenStartingWithMessages_ShouldShowMessagesUntilVoiceIsChosen()
    {
        var heatmap = RenderHeatmap(StatsMetric.Messages);

        Caption(heatmap).ShouldBe("Nachrichten pro Stunde");
        SelectedToggle(heatmap).ShouldBe("Nachrichten");

        heatmap.FindAll(".mud-toggle-item").Single(b => b.TextContent.Trim() == "Voice-Stunden").Click();

        heatmap.WaitForAssertion(() => Caption(heatmap).ShouldBe("Voice-Zeit pro Stunde"));
    }

    [Fact]
    public void ActivityHeatmap_OnAPhone_ShouldShowThreeHourBlocksOfVoiceTime()
    {
        Viewport.Breakpoint = Breakpoint.Xs;

        var heatmap = RenderHeatmap();

        var series = heatmap.FindComponents<ApexPointSeries<HeatmapBlock>>();
        series.Count.ShouldBe(7);
        series.ShouldAllBe(s => s.Instance.Items.Count() == 8);
        series[^1].Instance.Items.First().ShouldBe(new HeatmapBlock(0, 60 * (100 + 101 + 102)));
        series[^1].Instance.XValue(new HeatmapBlock(9, 0)).ShouldBe("09–12");
        Caption(heatmap).ShouldBe("Voice-Zeit pro 3 Stunden");
    }

    [Fact]
    public void ActivityHeatmap_OnAPhone_ShouldShowThreeHourBlocksOfMessages()
    {
        Viewport.Breakpoint = Breakpoint.Xs;

        var heatmap = RenderHeatmap(StatsMetric.Messages);

        var series = heatmap.FindComponents<ApexPointSeries<HeatmapBlock>>();
        series.ShouldAllBe(s => s.Instance.Items.Count() == 8);
        series[^1].Instance.Items.First().ShouldBe(new HeatmapBlock(0, 0 + 1 + 2));
        Caption(heatmap).ShouldBe("Nachrichten pro 3 Stunden");
    }

    private IRenderedComponent<ActivityHeatmap> RenderHeatmap(StatsMetric? initial = null) =>
        Render<ActivityHeatmap>(p =>
        {
            p.Add(h => h.Heatmap, _messages).Add(h => h.VoiceHeatmap, _voice);
            if (initial is { } metric)
            {
                p.Add(h => h.InitialMetric, metric);
            }
        });

    private static string Caption(IRenderedComponent<ActivityHeatmap> heatmap) =>
        heatmap.Find(".apollo-heatmap-caption").TextContent.Trim();

    private static string SelectedToggle(IRenderedComponent<ActivityHeatmap> heatmap) =>
        heatmap.FindAll(".mud-toggle-item").Single(b => b.ClassList.Contains("mud-toggle-item-selected")).TextContent.Trim();
}
