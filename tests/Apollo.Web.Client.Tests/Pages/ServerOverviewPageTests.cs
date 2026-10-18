using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Components.Stats;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Pages.Admin;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MudBlazor;

namespace Apollo.Web.Client.Tests.Pages;

/// <summary>The admin server dashboard (#37) against <see cref="MockServerStatsClient"/>.</summary>
public sealed class ServerOverviewPageTests : ApolloTestContext
{
    // 18 October 2026, 14:00 in Zurich.
    private static readonly DateOnly _today = new(2026, 10, 18);

    private readonly MockServerStatsClient _client = new();

    public ServerOverviewPageTests()
    {
        Services.AddSingleton<TimeProvider>(new FakeTimeProvider(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero)));
        Services.AddSingleton<IServerStatsClient>(_client);
    }

    [Fact]
    public void ServerOverview_ShouldLoadTheCurrentMonthByDefault()
    {
        var page = Render<ServerOverview>();

        page.WaitForAssertion(() => page.FindAll(".apollo-stat-tile").Count.ShouldBe(4));
        page.Find("h1").TextContent.Trim().ShouldBe("Server-Übersicht");
        _client.Requests.ShouldBe([(new DateOnly(2026, 10, 1), _today)]);
        page.Find(".apollo-range-text").TextContent.Trim().ShouldBe("01.10.2026 – 18.10.2026");
    }

    [Fact]
    public void ServerOverview_ShouldShowTheTotalsFormattedInGerman()
    {
        _client.Answer = (from, to) => MockServerStatsClient.Sample(from, to) with
        {
            Totals = new ActivityCountsDto(Messages: 12345, Characters: 0, ReactionsGiven: 1234, ReactionsReceived: 1000, VoiceSeconds: (3 * 3600) + (25 * 60)),
            Averages = new ActivityAveragesDto(Messages: 685.83, Characters: 0, ReactionsGiven: 68.6, ReactionsReceived: 0, VoiceSeconds: 683),
            ActiveDays = 17,
            ActiveUsers = 1042,
        };

        var page = Render<ServerOverview>();

        page.WaitForAssertion(() => Value(page, "Nachrichten").ShouldBe("12'345"));
        Subtext(page, "Nachrichten").ShouldBe("Ø 685.8 pro Tag");
        Value(page, "Voice-Stunden").ShouldBe("3 h 25 min");
        Subtext(page, "Voice-Stunden").ShouldBe("Ø 11 min pro Tag");
        Value(page, "Reaktionen").ShouldBe("1'234");
        Subtext(page, "Reaktionen").ShouldBe("Ø 68.6 pro Tag");
        Value(page, "Aktive Mitglieder").ShouldBe("1'042");
        Subtext(page, "Aktive Mitglieder").ShouldBe("An 17 von 18 Tagen aktiv");
    }

    [Fact]
    public void ServerOverview_ShouldPassTheStatsToTheSharedComponents()
    {
        var expected = MockServerStatsClient.Sample(new DateOnly(2026, 10, 1), _today);
        _client.Answer = (_, _) => expected;

        var page = Render<ServerOverview>();

        page.WaitForAssertion(() => page.FindComponent<ChannelRanking>().Instance.Channels.ShouldBeSameAs(expected.Channels));
        page.FindComponent<ActivityOverTimeChart>().Instance.Days.ShouldBeSameAs(expected.Daily);
        page.FindComponent<ActivityHeatmap>().Instance.Heatmap.ShouldBeSameAs(expected.Heatmap);
        page.FindComponent<DateRangeSelector>().Instance.Value.ShouldBe(new DateRangeSelection(DateRangePreset.Month, new DateOnly(2026, 10, 1), _today));
        page.FindAll(".apollo-stats-empty").ShouldBeEmpty();
    }

    [Fact]
    public void ServerOverview_WhenTheRangeHasNoData_ShouldShowTheEmptyStateInsteadOfCharts()
    {
        _client.Answer = MockServerStatsClient.Empty;

        var page = Render<ServerOverview>();

        page.WaitForAssertion(() => page.Find(".apollo-stats-empty .apollo-empty-title").TextContent.Trim().ShouldBe("Keine Daten im gewählten Zeitraum"));
        page.FindComponents<ActivityOverTimeChart>().ShouldBeEmpty();
        page.FindComponents<ChannelRanking>().ShouldBeEmpty();
        page.FindComponents<ActivityHeatmap>().ShouldBeEmpty();
        page.FindComponents<DateRangeSelector>().ShouldHaveSingleItem();
    }

    [Fact]
    public void ServerOverview_WhileLoading_ShouldShowProgress()
    {
        _client.Gate = new TaskCompletionSource();

        var page = Render<ServerOverview>();

        page.FindAll(".apollo-stats-loading").ShouldHaveSingleItem();
        page.FindAll(".apollo-stat-tile").ShouldBeEmpty();

        _client.Gate.SetResult();

        page.WaitForAssertion(() => page.FindAll(".apollo-stats-loading").ShouldBeEmpty());
        page.FindAll(".apollo-stat-tile").Count.ShouldBe(4);
    }

    [Fact]
    public void ServerOverview_WhenLoadFails_ShouldShowAnError()
    {
        _client.Failure = new HttpRequestException("boom");

        var page = Render<ServerOverview>();

        page.WaitForAssertion(() => page.Find(".apollo-stats-load-failed").TextContent.ShouldContain("Die Statistik konnte nicht geladen werden."));
        page.FindAll(".apollo-stats-loading").ShouldBeEmpty();
        page.FindAll(".apollo-stat-tile").ShouldBeEmpty();
        page.FindComponents<DateRangeSelector>().ShouldHaveSingleItem();
    }

    [Fact]
    public void ServerOverview_WhenYearChosen_ShouldReloadTheYearUpToToday()
    {
        var page = Render<ServerOverview>();
        page.WaitForAssertion(() => page.FindAll(".apollo-stat-tile").Count.ShouldBe(4));

        page.FindAll(".mud-toggle-item").Single(b => b.TextContent.Trim() == "Jahr").Click();

        page.WaitForAssertion(() => _client.Requests.ShouldBe([(new DateOnly(2026, 10, 1), _today), (new DateOnly(2026, 1, 1), _today)]));
        page.WaitForAssertion(() => page.Find(".apollo-range-text").TextContent.Trim().ShouldBe("01.01.2026 – 18.10.2026"));
    }

    [Fact]
    public async Task ServerOverview_WhenCustomRangePicked_ShouldReloadThatRange()
    {
        var page = Render<ServerOverview>();
        page.WaitForAssertion(() => page.FindAll(".apollo-stat-tile").Count.ShouldBe(4));

        page.FindAll(".mud-toggle-item").Single(b => b.TextContent.Trim() == "Benutzerdefiniert").Click();
        var picker = page.FindComponent<MudDateRangePicker>();
        await page.InvokeAsync(() => picker.Instance.DateRangeChanged.InvokeAsync(new DateRange(new DateTime(2026, 9, 3), new DateTime(2026, 9, 7))));

        page.WaitForAssertion(() => _client.Requests[^1].ShouldBe((new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 7))));
        page.WaitForAssertion(() => page.FindComponent<DateRangeSelector>().Instance.Value.ShouldBe(DateRangeSelection.Custom(new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 7))));
    }

    [Fact]
    public void ServerOverview_WhenAnOlderLoadAnswersLate_ShouldKeepTheNewerRange()
    {
        var first = new TaskCompletionSource();
        _client.Gate = first;
        var page = Render<ServerOverview>();

        _client.Gate = null;
        page.FindAll(".mud-toggle-item").Single(b => b.TextContent.Trim() == "Jahr").Click();
        page.WaitForAssertion(() => page.FindAll(".apollo-stat-tile").Count.ShouldBe(4));
        first.SetResult();

        page.WaitForAssertion(() => page.Find(".apollo-range-text").TextContent.Trim().ShouldBe("01.01.2026 – 18.10.2026"));
        page.FindComponent<ActivityOverTimeChart>().Instance.Days[0].Date.ShouldBe(new DateOnly(2026, 1, 1));
    }

    [Fact]
    public void ServerOverview_OnAPhone_ShouldStackTheTiles()
    {
        Viewport.Breakpoint = Breakpoint.Xs;

        var page = Render<ServerOverview>();

        page.WaitForAssertion(() => page.FindAll(".apollo-stat-tile").Count.ShouldBe(4));
        page.FindAll(".apollo-stats-tile-item").ShouldAllBe(item => item.ClassList.Contains("mud-grid-item-xs-12"));
    }

    private static string Value(IRenderedComponent<ServerOverview> page, string label) =>
        Tile(page, label).QuerySelector(".apollo-stat-value")!.TextContent.Trim();

    private static string Subtext(IRenderedComponent<ServerOverview> page, string label) =>
        Tile(page, label).QuerySelector(".apollo-stat-subtext")!.TextContent.Trim();

    private static AngleSharp.Dom.IElement Tile(IRenderedComponent<ServerOverview> page, string label) =>
        page.FindAll(".apollo-stat-tile").Single(t => t.QuerySelector(".apollo-stat-label")!.TextContent.Trim() == label);
}
