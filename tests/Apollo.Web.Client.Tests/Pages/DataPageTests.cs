using Apollo.Testing;
using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Pages.Admin.Events;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using MudBlazor;
using MudBlazor.Extensions;
using DataPage = Apollo.Web.Client.Pages.Admin.Data;

namespace Apollo.Web.Client.Tests.Pages;

/// <summary>The admin data page (#32) against <see cref="MockDataOverviewClient"/>.</summary>
public sealed class DataPageTests : ApolloTestContext
{
    // TestTime.Now is 15 January 2026, 10:00 in Zurich.
    private static readonly DateOnly _today = new(2026, 1, 15);

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private readonly MockDataOverviewClient _client;

    public DataPageTests()
    {
        _client = new MockDataOverviewClient(_time);
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddSingleton<IDataOverviewClient>(_client);
    }

    [Fact]
    public void DataPage_ShouldRenderCountsAndTrackingTiles()
    {
        // Messages: 1 on the first day up to 30 today; reactions: 2 every day; the rest nothing.
        var from = _today.AddDays(-29);
        _client.Counts = new EventCountsDto(from, _today, [.. Enum.GetValues<DataEventType>().Select(type => Table(type, from, type switch
        {
            DataEventType.Message => i => i + 1,
            DataEventType.Reaction => _ => 2,
            _ => _ => 0,
        }))]);

        var page = Render<DataPage>();

        page.WaitForAssertion(() => Tile(page, "Erfasste Mitglieder").ShouldContain("168"));
        page.Find("h1").TextContent.Trim().ShouldBe("Daten");
        Tile(page, "Erfasste Mitglieder").ShouldContain("Haben die Tracking-Rolle");
        Tile(page, "Nicht erfasste Mitglieder").ShouldContain("46");
        Tile(page, "Ereignisse heute").ShouldContain("32");
        Tile(page, "Ereignisse 30 Tage").ShouldContain("525");
        Tile(page, "Ereignisse 30 Tage").ShouldContain("Seit 17.12.2025");
        CountsRow(page, "message_events").ShouldBe(["Nachrichten message_events", "30", "29", "189", "465"]);
        CountsRow(page, "reaction_events").ShouldBe(["Reaktionen reaction_events", "2", "2", "14", "60"]);
        CountsRow(page, "membership_events").ShouldBe(["Mitgliedschaft membership_events", "0", "0", "0", "0"]);
        page.FindComponent<EventsPerDayChart>().Instance.Counts.ShouldBeSameAs(_client.Counts);
        page.FindAll(".apollo-data-mode-warning").ShouldBeEmpty();
    }

    [Theory]
    [InlineData(RoleSettingsMode.NoTrackingRole, "Keine Tracking-Rolle festgelegt: niemand wird erfasst.")]
    [InlineData(RoleSettingsMode.TrackingRoleDeleted, "Die Tracking-Rolle wurde gelöscht: niemand wird erfasst.")]
    public void DataPage_WhenNobodyIsTracked_ShouldWarn(RoleSettingsMode mode, string warning)
    {
        _client.MemberTracking = [new MemberTrackingDto("Discord", 0, 214, 214, mode)];

        var page = Render<DataPage>();

        page.WaitForAssertion(() => page.Find(".apollo-data-mode-warning").TextContent.ShouldContain(warning));
        Tile(page, "Erfasste Mitglieder").ShouldContain("0");
        Tile(page, "Nicht erfasste Mitglieder").ShouldContain("214");
    }

    [Fact]
    public void DataPage_WhenGuildNotSyncedYet_ShouldShowPlaceholderTiles()
    {
        _client.MemberTracking = [];

        var page = Render<DataPage>();

        page.WaitForAssertion(() => Tile(page, "Erfasste Mitglieder").ShouldContain("Noch keine Mitglieder synchronisiert"));
        Tile(page, "Erfasste Mitglieder").ShouldContain("–");
    }

    [Fact]
    public void DataPage_WhenLoadFails_ShouldSayItInline()
    {
        _client.Failure = new HttpRequestException("down");

        var page = Render<DataPage>();

        page.WaitForAssertion(() => page.Find(".apollo-data-overview-failed").TextContent.ShouldContain("konnte nicht geladen werden"));
        page.Find(".apollo-data-events-failed").TextContent.ShouldContain("konnten nicht geladen werden");
        page.Find("h1").TextContent.Trim().ShouldBe("Daten");
    }

    [Fact]
    public void EventBrowser_WhenALoadSucceedsAfterAFailure_ShouldHideTheAlert()
    {
        _client.Failure = new HttpRequestException("down");
        var page = Render<DataPage>();
        page.WaitForAssertion(() => page.FindAll(".apollo-data-events-failed").ShouldHaveSingleItem());
        _client.Failure = null;

        page.Find(".apollo-data-apply").Click();

        page.WaitForAssertion(() => EventRows(page).Count.ShouldBe(3));
        page.FindAll(".apollo-data-events-failed").ShouldBeEmpty();
    }

    [Fact]
    public void EventBrowser_ShouldRequestTheFirstPageOfMessagesOfTheLast24Hours()
    {
        var page = Render<DataPage>();

        page.WaitForAssertion(() => _client.EventRequests.ShouldHaveSingleItem().ShouldBe(new RawEventFilter(DataEventType.Message)));
    }

    [Fact]
    public void EventBrowser_ShouldShowTheRowsWithZurichTimes()
    {
        var page = Render<DataPage>();

        page.WaitForAssertion(() => EventRows(page).Count.ShouldBe(3));
        EventRows(page)[0].ShouldBe(["15.01.2026 09:57:00", "Nachricht · erstellt", "Anna", "allgemein", "42 Zeichen · 8 Wörter · 1 Anhang · Text, Anhang"]);
        EventRows(page)[1][4].ShouldEndWith("Antwort an Anna");
        EventRows(page)[2].ShouldBe(["15.01.2026 08:00:00", "Nachricht · gelöscht", "Chris", "allgemein", "–"]);
    }

    [Fact]
    public async Task EventBrowser_WhenFilterChanged_ShouldRequestFirstPage()
    {
        _client.Events = filter => new RawEventPageDto(120, [.. Enumerable.Range(0, filter.PageSize).Select(i => new RawEventDto(
            1000 - ((filter.Page - 1) * filter.PageSize) - i, DataEventType.Message, TestTime.Now.AddMinutes(-i), MockDataOverviewClient.Anna, null)
        {
            Message = new("Create", 1, 1, 0, 0, 0, 0, 0, false, null, null, null, false),
        })]);
        var page = Render<DataPage>();
        page.WaitForAssertion(() => EventRows(page).Count.ShouldBe(50));
        page.Find(".apollo-data-events .mud-table-pagination-actions button:nth-last-child(2)").Click();
        page.WaitForAssertion(() => _client.EventRequests[^1].Page.ShouldBe(2));

        await page.InvokeAsync(() => page.FindComponent<MudSelect<DataEventType>>().Instance.ValueChanged.InvokeAsync(DataEventType.Reaction));
        await page.InvokeAsync(() => page.FindComponent<MudAutocomplete<EventUserDto>>().Instance.ValueChanged.InvokeAsync(MockDataOverviewClient.Ben));
        await page.InvokeAsync(() => page.FindComponents<MudDatePicker>()[0].Instance.DateChanged.InvokeAsync(new DateTime(2026, 1, 10)));
        await page.InvokeAsync(() => page.FindComponents<MudDatePicker>()[1].Instance.DateChanged.InvokeAsync(new DateTime(2026, 1, 12)));
        _client.EventRequests[^1].Page.ShouldBe(2);
        page.Find(".apollo-data-apply").Click();

        page.WaitForAssertion(() => _client.EventRequests[^1].ShouldBe(new RawEventFilter(
            DataEventType.Reaction,
            MockDataOverviewClient.Ben.Id,
            From: new DateTimeOffset(2026, 1, 9, 23, 0, 0, TimeSpan.Zero),
            To: new DateTimeOffset(2026, 1, 12, 23, 0, 0, TimeSpan.Zero))));
    }

    [Fact]
    public async Task EventBrowser_WhenReset_ShouldRequestTheDefaultFilter()
    {
        var page = Render<DataPage>();
        page.WaitForAssertion(() => _client.EventRequests.Count.ShouldBe(1));
        await page.InvokeAsync(() => page.FindComponent<MudSelect<DataEventType>>().Instance.ValueChanged.InvokeAsync(DataEventType.Membership));
        page.Find(".apollo-data-apply").Click();
        page.WaitForAssertion(() => _client.EventRequests.Count.ShouldBe(2));

        page.Find(".apollo-data-reset").Click();

        page.WaitForAssertion(() => _client.EventRequests.Count.ShouldBe(3));
        _client.EventRequests[^1].ShouldBe(new RawEventFilter(DataEventType.Message));
        page.FindComponent<MudSelect<DataEventType>>().Instance.GetState(x => x.Value).ShouldBe(DataEventType.Message);
    }

    [Fact]
    public async Task EventBrowser_WhenTypeHasNoChannel_ShouldDisableAndClearTheChannelFilter()
    {
        var page = Render<DataPage>();
        await page.InvokeAsync(() => page.FindComponent<MudAutocomplete<EventChannelDto>>().Instance.ValueChanged.InvokeAsync(MockDataOverviewClient.Memes));

        await page.InvokeAsync(() => page.FindComponent<MudSelect<DataEventType>>().Instance.ValueChanged.InvokeAsync(DataEventType.Membership));
        page.Find(".apollo-data-apply").Click();

        page.WaitForAssertion(() => _client.EventRequests[^1].ShouldBe(new RawEventFilter(DataEventType.Membership)));
        page.Find(".apollo-data-channel input").HasAttribute("disabled").ShouldBeTrue();
    }

    [Fact]
    public async Task EventBrowser_WhenUserNull_ShouldShowNichtErfasst()
    {
        var page = Render<DataPage>();
        await page.InvokeAsync(() => page.FindComponent<MudSelect<DataEventType>>().Instance.ValueChanged.InvokeAsync(DataEventType.Reaction));

        page.Find(".apollo-data-apply").Click();

        page.WaitForAssertion(() => EventRows(page).Count.ShouldBe(2));
        EventRows(page)[0][4].ShouldBe(":lachen: · auf Nachricht von Anna");
        EventRows(page)[1][4].ShouldBe("👍 · auf Nachricht von Nicht erfasst");
    }

    [Fact]
    public void EventBrowser_WhenNoEvents_ShouldShowEmptyState()
    {
        _client.Events = _ => new RawEventPageDto(0, []);

        var page = Render<DataPage>();

        page.WaitForAssertion(() => page.Find(".apollo-data-events .apollo-empty-title").TextContent.Trim().ShouldBe("Keine Ereignisse gefunden"));
    }

    private static EventTableCountDto Table(DataEventType type, DateOnly from, Func<int, int> count)
    {
        var days = Enumerable.Range(0, 30).Select(i => new DailyEventCountDto(from.AddDays(i), count(i))).ToList();
        return new EventTableCountDto(type, days.Sum(d => d.Count), days);
    }

    private static string Tile(IRenderedComponent<DataPage> page, string label) =>
        page.FindAll(".apollo-stat-tile").Single(t => t.QuerySelector(".apollo-stat-label")!.TextContent.Trim() == label).TextContent;

    private static List<string> CountsRow(IRenderedComponent<DataPage> page, string table) =>
        [.. page.FindAll(".apollo-data-counts tbody tr").Single(r => r.TextContent.Contains(table, StringComparison.Ordinal))
            .QuerySelectorAll("td").Select(td => string.Join(' ', td.TextContent.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)))];

    private static List<List<string>> EventRows(IRenderedComponent<DataPage> page) =>
        [.. page.FindAll(".apollo-data-events tbody tr:not(.mud-table-empty-row)")
            .Where(r => r.QuerySelector(".apollo-empty-title") is null)
            .Select(r => r.QuerySelectorAll("td").Select(td => td.TextContent.Trim()).ToList())];
}
