using Apollo.Web.Client.Components.Stats;

namespace Apollo.Web.Client.Tests.Components.Stats;

public sealed class HeatmapBlocksTests
{
    private static readonly IReadOnlyList<IReadOnlyList<long>> _heatmap =
        [.. Enumerable.Range(0, 7).Select(day => (IReadOnlyList<long>)[.. Enumerable.Range(0, 24).Select(hour => (long)((day * 100) + hour))])];

    [Fact]
    public void Group_WhenOneHourPerBlock_ShouldKeepEveryHour()
    {
        var days = HeatmapBlocks.Group(_heatmap, hoursPerBlock: 1);

        days.Count.ShouldBe(7);
        days[1].Select(b => (b.StartHour, b.Count)).Take(3).ShouldBe([(0, 100L), (1, 101L), (2, 102L)]);
        days[6].Count.ShouldBe(24);
    }

    [Fact]
    public void Group_WhenThreeHoursPerBlock_ShouldSumEightBlocksPerDay()
    {
        var days = HeatmapBlocks.Group(_heatmap, hoursPerBlock: 3);

        days.Count.ShouldBe(7);
        days.ShouldAllBe(d => d.Count == 8);
        days[0].Select(b => b.StartHour).ShouldBe([0, 3, 6, 9, 12, 15, 18, 21]);
        days[0][0].Count.ShouldBe(0 + 1 + 2);
        days[2][7].Count.ShouldBe(221 + 222 + 223);
    }

    [Fact]
    public void Group_WhenHoursDoNotDivideTheDay_ShouldThrow() =>
        Should.Throw<ArgumentOutOfRangeException>(() => HeatmapBlocks.Group(_heatmap, hoursPerBlock: 5));
}
