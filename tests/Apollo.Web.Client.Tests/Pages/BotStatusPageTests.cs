using Apollo.Testing;
using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Pages.Admin;
using Apollo.Web.Client.Pages.Admin.Status;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Client.Tests.Pages;

/// <summary>The admin bot status page (#31) against <see cref="MockBotStatusClient"/>.</summary>
public sealed class BotStatusPageTests : ApolloTestContext
{
    // TestTime.Now is 10:00 in Zurich.
    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private readonly MockBotStatusClient _client;

    public BotStatusPageTests()
    {
        _client = new MockBotStatusClient(_time);
        Services.AddSingleton<TimeProvider>(_time);
        Services.AddSingleton<IBotStatusClient>(_client);
    }

    [Fact]
    public void BotStatusPage_WhenConnected_ShouldShowTheTiles()
    {
        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-connection").TextContent.Trim().ShouldBe("Verbunden"));
        page.Find("h1").TextContent.Trim().ShouldBe("Bot-Status");
        page.Find(".apollo-bot-checked").TextContent.Trim().ShouldBe("Aktualisiert um 10:00:00");
        page.Find(".apollo-bot-connection").ClassList.ShouldContain("mud-success-text");
        Tile(page, "Verbindung").ShouldContain("Letztes Lebenszeichen: 09:59:53");
        Tile(page, "Gateway-Latenz").ShouldContain("84 ms");
        Tile(page, "Letztes Ereignis").ShouldContain("vor 12 s");
        Tile(page, "Offene Voice-Sessions").ShouldContain("3");
        page.FindComponent<EventsPerHourChart>().Instance.Hours.Count.ShouldBe(24);
        page.FindAll(".apollo-bot-stale").ShouldBeEmpty();
        page.FindAll(".apollo-bot-offline").ShouldBeEmpty();
    }

    [Fact]
    public void BotStatusPage_WhenStale_ShouldShowWarning()
    {
        _client.Status = _client.Status with { LastHeartbeatAt = TestTime.Now.AddMinutes(-28), IsStale = true };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-stale").TextContent.ShouldContain("Kein Lebenszeichen vom Bot seit 09:32 (vor 28 min)."));
        page.Find(".apollo-bot-stale").TextContent.ShouldContain("Prüfe die Logs");
        page.Find(".apollo-bot-connection").TextContent.Trim().ShouldBe("Verbunden?");
        page.Find(".apollo-bot-connection").ClassList.ShouldContain("mud-warning-text");
        page.FindAll(".apollo-bot-offline").ShouldBeEmpty();
    }

    [Fact]
    public void BotStatusPage_WhenStaleSinceYesterday_ShouldShowTheDate()
    {
        _client.Status = _client.Status with { LastHeartbeatAt = TestTime.Now.AddHours(-12), IsStale = true };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-stale").TextContent.ShouldContain("seit 14.01.2026 22:00 (vor 12 h)."));
    }

    [Fact]
    public void BotStatusPage_WhenDisconnected_ShouldShowGetrennt()
    {
        _client.Status = _client.Status with { Connected = false, GatewayLatencyMs = null };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-connection").TextContent.Trim().ShouldBe("Getrennt"));
        page.Find(".apollo-bot-connection").ClassList.ShouldContain("mud-error-text");
        page.Find(".apollo-bot-offline").TextContent.ShouldContain("Der Bot ist nicht verbunden.");
        Tile(page, "Gateway-Latenz").ShouldContain("–");
    }

    [Fact]
    public void BotStatusPage_WhenNoEventSinceStart_ShouldSayNoneYet()
    {
        _client.Status = _client.Status with { LastEventAt = null };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => Tile(page, "Letztes Ereignis").ShouldContain("Seit dem Start des Bots noch keines"));
    }

    [Fact]
    public void BotStatusPage_WhenNoHeartbeat_ShouldShowEmptyState()
    {
        _client.Status = _client.Status with { LastHeartbeatAt = null, Connected = false, IsStale = false };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-empty-title").TextContent.Trim().ShouldBe("Noch kein Heartbeat empfangen"));
        page.FindAll(".apollo-bot-connection").ShouldBeEmpty();
        page.FindAll(".apollo-bot-offline").ShouldBeEmpty();
    }

    [Fact]
    public void BotStatusPage_WhenLoadFails_ShouldSayItInline()
    {
        _client.Failure = new HttpRequestException("down");

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-load-failed").TextContent.ShouldContain("konnte nicht geladen werden"));
    }

    [Fact]
    public void BotStatusPage_WhenTimerTicks_ShouldReloadData()
    {
        var page = Render<BotStatus>();
        page.WaitForAssertion(() => _client.Calls.ShouldBe(1));
        _client.Status = _client.Status with { Connected = false };

        _time.Advance(TimeSpan.FromSeconds(30));

        page.WaitForAssertion(() => page.Find(".apollo-bot-connection").TextContent.Trim().ShouldBe("Getrennt"));
        _client.Calls.ShouldBe(2);
    }

    [Fact]
    public void BotStatusPage_BeforeThirtySeconds_ShouldNotReload()
    {
        var page = Render<BotStatus>();
        page.WaitForAssertion(() => _client.Calls.ShouldBe(1));

        _time.Advance(TimeSpan.FromSeconds(29));

        _client.Calls.ShouldBe(1);
    }

    [Fact]
    public void BotStatusPage_WhenRefreshClicked_ShouldReloadData()
    {
        var page = Render<BotStatus>();
        page.WaitForAssertion(() => _client.Calls.ShouldBe(1));
        _client.Status = _client.Status with { OpenVoiceSessions = 5 };

        page.Find(".apollo-bot-refresh").Click();

        page.WaitForAssertion(() => Tile(page, "Offene Voice-Sessions").ShouldContain("5"));
        _client.Calls.ShouldBe(2);
    }

    [Fact]
    public async Task BotStatusPage_WhenDisposed_ShouldStopRefreshing()
    {
        var page = Render<BotStatus>();
        page.WaitForAssertion(() => _client.Calls.ShouldBe(1));

        await DisposeComponentsAsync();
        _time.Advance(TimeSpan.FromMinutes(2));

        _client.Calls.ShouldBe(1);
    }

    private static string Tile(IRenderedComponent<BotStatus> page, string label) =>
        page.FindAll(".apollo-stat-tile").Single(t => t.QuerySelector(".apollo-stat-label")!.TextContent.Trim() == label).TextContent;
}
