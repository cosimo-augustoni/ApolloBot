using ApexCharts;
using Apollo.Web.Client.Components.Stats;
using Apollo.Web.Client.Contracts;
using Bunit;

namespace Apollo.Web.Client.Tests.Components.Stats;

/// <summary>Activity per day (#37): messages, or voice hours on request.</summary>
public sealed class ActivityOverTimeChartTests : ApolloTestContext
{
    private static readonly DailyActivityDto[] _days =
    [
        new(new DateOnly(2026, 10, 1), new ActivityCountsDto(12, 300, 2, 2, 5400)),
        new(new DateOnly(2026, 10, 2), new ActivityCountsDto(7, 100, 0, 0, 0)),
    ];

    [Fact]
    public void ActivityOverTimeChart_ShouldShowMessagesPerDay()
    {
        var chart = Render<ActivityOverTimeChart>(p => p.Add(c => c.Days, _days));

        var series = chart.FindComponent<ApexPointSeries<DailyActivityDto>>().Instance;
        series.Name.ShouldBe("Nachrichten");
        series.Items.ShouldBe(_days);
        series.YValue(_days[0]).ShouldBe(12m);
        series.XValue(_days[0]).ShouldBe("01.10.");
    }

    [Fact]
    public void ActivityOverTimeChart_WhenVoiceChosen_ShouldShowVoiceHoursPerDay()
    {
        var chart = Render<ActivityOverTimeChart>(p => p.Add(c => c.Days, _days));

        chart.FindAll(".mud-toggle-item").Single(b => b.TextContent.Trim() == "Voice-Stunden").Click();

        chart.WaitForAssertion(() => chart.FindComponent<ApexPointSeries<DailyActivityDto>>().Instance.Name.ShouldBe("Voice-Stunden"));
        var series = chart.FindComponent<ApexPointSeries<DailyActivityDto>>().Instance;
        series.YValue(_days[0]).ShouldBe(1.5m);
    }
}
