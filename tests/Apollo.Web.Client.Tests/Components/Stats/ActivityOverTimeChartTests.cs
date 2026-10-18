using ApexCharts;
using Apollo.Web.Client.Components.Stats;
using Apollo.Web.Client.Contracts;
using Bunit;

namespace Apollo.Web.Client.Tests.Components.Stats;

/// <summary>Activity per day (#37): voice hours, or messages on request (#111).</summary>
public sealed class ActivityOverTimeChartTests : ApolloTestContext
{
    private static readonly DailyActivityDto[] _days =
    [
        new(new DateOnly(2026, 10, 1), new ActivityCountsDto(12, 300, 2, 2, 5400)),
        new(new DateOnly(2026, 10, 2), new ActivityCountsDto(7, 100, 0, 0, 0)),
    ];

    [Fact]
    public void ActivityOverTimeChart_ByDefault_ShouldShowVoiceHoursPerDay()
    {
        var chart = Render<ActivityOverTimeChart>(p => p.Add(c => c.Days, _days));

        var series = chart.FindComponent<ApexPointSeries<DailyActivityDto>>().Instance;
        series.Name.ShouldBe("Voice-Stunden");
        series.Items.ShouldBe(_days);
        series.YValue(_days[0]).ShouldBe(1.5m);
        series.XValue(_days[0]).ShouldBe("01.10.");
        chart.FindAll(".mud-toggle-item").Select(b => b.TextContent.Trim()).ShouldBe(["Voice-Stunden", "Nachrichten"]);
    }

    [Fact]
    public void ActivityOverTimeChart_WhenMessagesChosen_ShouldShowMessagesPerDay()
    {
        var chart = Render<ActivityOverTimeChart>(p => p.Add(c => c.Days, _days));

        chart.FindAll(".mud-toggle-item").Single(b => b.TextContent.Trim() == "Nachrichten").Click();

        chart.WaitForAssertion(() => chart.FindComponent<ApexPointSeries<DailyActivityDto>>().Instance.Name.ShouldBe("Nachrichten"));
        var series = chart.FindComponent<ApexPointSeries<DailyActivityDto>>().Instance;
        series.YValue(_days[0]).ShouldBe(12m);
    }

    [Fact]
    public void ActivityOverTimeChart_WhenStartingWithMessages_ShouldShowMessagesPerDay()
    {
        var chart = Render<ActivityOverTimeChart>(p => p.Add(c => c.Days, _days).Add(c => c.InitialMetric, StatsMetric.Messages));

        chart.FindComponent<ApexPointSeries<DailyActivityDto>>().Instance.Name.ShouldBe("Nachrichten");
    }
}
