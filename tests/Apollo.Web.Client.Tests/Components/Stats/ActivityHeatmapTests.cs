using ApexCharts;
using Apollo.Web.Client.Components.Stats;
using Bunit;
using MudBlazor;

namespace Apollo.Web.Client.Tests.Components.Stats;

/// <summary>The weekday × hour heatmap (#37): 24 hours on wide screens, 8 blocks of 3 hours below 600px.</summary>
public sealed class ActivityHeatmapTests : ApolloTestContext
{
    private static readonly IReadOnlyList<IReadOnlyList<long>> _heatmap =
        [.. Enumerable.Range(0, 7).Select(day => (IReadOnlyList<long>)[.. Enumerable.Range(0, 24).Select(hour => (long)(day + hour))])];

    [Fact]
    public void ActivityHeatmap_OnAWideScreen_ShouldShowEveryHourWithMondayOnTop()
    {
        var heatmap = Render<ActivityHeatmap>(p => p.Add(h => h.Heatmap, _heatmap));

        var series = heatmap.FindComponents<ApexPointSeries<HeatmapBlock>>();
        // ApexCharts draws the first series at the bottom.
        series.Select(s => s.Instance.Name).ShouldBe(["So", "Sa", "Fr", "Do", "Mi", "Di", "Mo"]);
        series.ShouldAllBe(s => s.Instance.Items.Count() == 24);
        series[^1].Instance.Items.First().ShouldBe(new HeatmapBlock(0, 0));
        series[^1].Instance.XValue(new HeatmapBlock(9, 0)).ShouldBe("09");
    }

    [Fact]
    public void ActivityHeatmap_OnAPhone_ShouldShowThreeHourBlocks()
    {
        Viewport.Breakpoint = Breakpoint.Xs;

        var heatmap = Render<ActivityHeatmap>(p => p.Add(h => h.Heatmap, _heatmap));

        var series = heatmap.FindComponents<ApexPointSeries<HeatmapBlock>>();
        series.Count.ShouldBe(7);
        series.ShouldAllBe(s => s.Instance.Items.Count() == 8);
        series[^1].Instance.Items.First().ShouldBe(new HeatmapBlock(0, 0 + 1 + 2));
        series[^1].Instance.XValue(new HeatmapBlock(9, 0)).ShouldBe("09–12");
    }
}
