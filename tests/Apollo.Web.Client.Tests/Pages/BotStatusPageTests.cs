using ApexCharts;
using Apollo.Testing;
using Apollo.Web.Client.Api;
using Apollo.Web.Client.Api.Mock;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Client.Pages.Admin;
using Apollo.Web.Client.Pages.Admin.Status;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Client.Tests.Pages;

/// <summary>The admin bot status page (#31, #91) against <see cref="MockBotStatusClient"/>.</summary>
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
        _client.Status = _client.Status with { Connected = false, GatewayLatencyMeanMs = null };

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
    public void BotStatusPage_WhenFirstLoadFailsWithNonHttpError_ShouldShowTheFailureStateNotTheErrorUi()
    {
        _client.Failure = new System.Text.Json.JsonException("<!DOCTYPE html> is no JSON");

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-load-failed").TextContent.ShouldContain("konnte nicht geladen werden"));
        page.Find("h1").TextContent.Trim().ShouldBe("Bot-Status");
    }

    [Fact]
    public void BotStatusPage_WhenATickTimesOut_ShouldShowTheFailureAndKeepRefreshing()
    {
        var page = Render<BotStatus>();
        page.WaitForAssertion(() => _client.Calls.ShouldBe(1));
        _client.Failure = new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.");

        _time.Advance(TimeSpan.FromSeconds(30));

        page.WaitForAssertion(() => page.Find(".apollo-bot-load-failed").ShouldNotBeNull());
        page.Find(".apollo-bot-connection").TextContent.Trim().ShouldBe("Verbunden");
        _client.Failure = null;
        _client.Status = _client.Status with { Connected = false };

        _time.Advance(TimeSpan.FromSeconds(30));

        page.WaitForAssertion(() => page.Find(".apollo-bot-connection").TextContent.Trim().ShouldBe("Getrennt"));
        page.FindAll(".apollo-bot-load-failed").ShouldBeEmpty();
        _client.Calls.ShouldBe(3);
    }

    [Fact]
    public void BotStatusPage_WhenATickFailsWithNonHttpError_ShouldKeepRefreshing()
    {
        var page = Render<BotStatus>();
        page.WaitForAssertion(() => _client.Calls.ShouldBe(1));
        _client.Failure = new InvalidOperationException("GET api/admin/bot-status returned no content.");

        _time.Advance(TimeSpan.FromSeconds(30));

        page.WaitForAssertion(() => page.Find(".apollo-bot-load-failed").ShouldNotBeNull());
        _client.Failure = null;

        _time.Advance(TimeSpan.FromSeconds(30));

        page.WaitForAssertion(() => page.FindAll(".apollo-bot-load-failed").ShouldBeEmpty());
        _client.Calls.ShouldBe(3);
    }

    [Fact]
    public void BotStatusPage_WhileALoadRuns_ShouldDisableRefresh()
    {
        var client = new PendingBotStatusClient(_client.Status);
        Services.AddSingleton<IBotStatusClient>(client);
        var page = Render<BotStatus>();
        client.Complete(0);
        page.WaitForAssertion(() => page.Find(".apollo-bot-connection").TextContent.Trim().ShouldBe("Verbunden"));

        page.Find(".apollo-bot-refresh").Click();

        page.WaitForAssertion(() => page.Find(".apollo-bot-refresh").HasAttribute("disabled").ShouldBeTrue());
        client.Complete(1);
        page.WaitForAssertion(() => page.Find(".apollo-bot-refresh").HasAttribute("disabled").ShouldBeFalse());
    }

    [Fact]
    public void BotStatusPage_WhenAClickAndATickOverlap_ShouldKeepTheNewerAnswer()
    {
        var client = new PendingBotStatusClient(_client.Status);
        Services.AddSingleton<IBotStatusClient>(client);
        var page = Render<BotStatus>();
        client.Complete(0);
        page.WaitForAssertion(() => page.Find(".apollo-bot-connection").TextContent.Trim().ShouldBe("Verbunden"));
        page.Find(".apollo-bot-refresh").Click();
        page.WaitForAssertion(() => client.Calls.ShouldBe(2));
        _time.Advance(TimeSpan.FromSeconds(30));
        page.WaitForAssertion(() => client.Calls.ShouldBe(3));

        client.Complete(2, _client.Status with { Connected = false });
        page.WaitForAssertion(() => page.Find(".apollo-bot-connection").TextContent.Trim().ShouldBe("Getrennt"));
        client.Complete(1, _client.Status with { Connected = true });

        page.WaitForAssertion(() => page.Find(".apollo-bot-refresh").HasAttribute("disabled").ShouldBeFalse());
        page.Find(".apollo-bot-connection").TextContent.Trim().ShouldBe("Getrennt");
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
        _client.Status = _client.Status with { OpenVoiceSessions = [.. _client.Status.OpenVoiceSessions, new OpenVoiceSessionDto("Dana", _client.Status.OpenVoiceSessions[2].ChannelId, "Gaming", TestTime.Now)] };

        page.Find(".apollo-bot-refresh").Click();

        page.WaitForAssertion(() => Tile(page, "Offene Voice-Sessions").ShouldContain("4"));
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

    [Fact]
    public void BotStatusPage_WhenNoTrackingRole_ShouldShowTheBannerWithALinkToTheSettings()
    {
        _client.Status = _client.Status with { NoTrackingRole = true };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-no-tracking-role").TextContent.ShouldContain("Keine Tracking-Rolle festgelegt: Es wird niemand erfasst."));
        var link = page.Find(".apollo-bot-no-tracking-role a");
        link.GetAttribute("href").ShouldBe("/admin/settings");
        link.TextContent.Trim().ShouldBe("Tracking-Rolle festlegen");
    }

    [Fact]
    public void BotStatusPage_WhenATrackingRoleIsSet_ShouldNotShowTheBanner()
    {
        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-connection").ShouldNotBeNull());
        page.FindAll(".apollo-bot-no-tracking-role").ShouldBeEmpty();
    }

    [Fact]
    public void BotStatusPage_WhenOpenVoiceSessionsInTwoChannels_ShouldListThemAndCountTheChannels()
    {
        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.FindAll(".apollo-bot-voice-session").Count.ShouldBe(3));
        page.FindAll(".apollo-bot-voice-session").Select(r => r.QuerySelectorAll("td").Select(c => c.TextContent.Trim()).ToArray()).ShouldBe(
        [
            ["Anna", "Voice-Lounge", "09:10"],
            ["Ben", "Voice-Lounge", "09:42"],
            ["Chris", "Gaming", "09:45"],
        ]);
        page.Find(".apollo-bot-voice-sessions").TextContent.ShouldContain("Mitglied");
        Tile(page, "Offene Voice-Sessions").ShouldContain("3");
        Tile(page, "Offene Voice-Sessions").ShouldContain("In 2 Kanälen");
    }

    [Fact]
    public void BotStatusPage_WhenAllOpenVoiceSessionsInOneChannel_ShouldSayOneChannel()
    {
        _client.Status = _client.Status with { OpenVoiceSessions = [new OpenVoiceSessionDto("Anna", Guid.NewGuid(), "Voice-Lounge", TestTime.Now.AddMinutes(-50))] };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => Tile(page, "Offene Voice-Sessions").ShouldContain("In 1 Kanal"));
    }

    [Fact]
    public void BotStatusPage_WhenAVoiceSessionStartedYesterday_ShouldShowTheDate()
    {
        _client.Status = _client.Status with { OpenVoiceSessions = [new OpenVoiceSessionDto("Anna", Guid.NewGuid(), "Voice-Lounge", TestTime.Now.AddHours(-12))] };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-voice-session").TextContent.ShouldContain("14.01.2026 22:00"));
    }

    [Fact]
    public void BotStatusPage_WhenNoOpenVoiceSessions_ShouldShowTheEmptyStateAndNoTileSubtext()
    {
        _client.Status = _client.Status with { OpenVoiceSessions = [] };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-voice-sessions").TextContent.ShouldContain("Gerade ist niemand in einem Voice-Kanal."));
        page.FindAll(".apollo-bot-voice-session").ShouldBeEmpty();
        Tile(page, "Offene Voice-Sessions").ShouldContain("0");
        TileElement(page, "Offene Voice-Sessions").QuerySelector(".apollo-stat-subtext").ShouldBeNull();
    }

    [Fact]
    public void BotStatusPage_WhenTheLastRecordedEventHasAChannel_ShouldShowTypeAndChannel()
    {
        var page = Render<BotStatus>();

        page.WaitForAssertion(() => TileSubtext(page, "Letztes Ereignis").ShouldBe("Nachricht in #allgemein"));
        Tile(page, "Letztes Ereignis").ShouldContain("vor 12 s");
    }

    [Fact]
    public void BotStatusPage_WhenTheLastRecordedEventHasNoChannel_ShouldShowTheTypeOnly()
    {
        _client.Status = _client.Status with { LastRecordedEvent = new LastRecordedEventDto(DataEventType.Membership, TestTime.Now.AddMinutes(-2), null) };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => TileSubtext(page, "Letztes Ereignis").ShouldBe("Mitgliedschaft"));
    }

    [Fact]
    public void BotStatusPage_WhenNothingRecorded_ShouldShowWhenTheLastEventWasReceived()
    {
        _client.Status = _client.Status with { LastRecordedEvent = null };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => TileSubtext(page, "Letztes Ereignis").ShouldBe("09:59"));
    }

    [Fact]
    public void BotStatusPage_WhenDisconnected_ShouldShowTheLastConnection()
    {
        _client.Status = _client.Status with { Connected = false, LastConnectedAt = TestTime.Now.AddMinutes(-5) };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-offline").TextContent.ShouldContain("Letzte Verbindung um 09:55."));
    }

    [Fact]
    public void BotStatusPage_WhenDisconnectedAndNeverConnected_ShouldLeaveTheTimeOut()
    {
        _client.Status = _client.Status with { Connected = false, LastConnectedAt = null };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.Find(".apollo-bot-offline").TextContent.ShouldContain("Der Bot ist nicht verbunden."));
        page.Find(".apollo-bot-offline").TextContent.ShouldNotContain("Letzte Verbindung");
    }

    [Fact]
    public void BotStatusPage_ShouldShowTheLatencyAsTheMeanOfTheLastFiveMinutes()
    {
        _client.Status = _client.Status with { GatewayLatencyMeanMs = 91 };

        var page = Render<BotStatus>();

        page.WaitForAssertion(() => Tile(page, "Gateway-Latenz").ShouldContain("91 ms"));
        TileSubtext(page, "Gateway-Latenz").ShouldBe("Mittel der letzten 5 min");
    }

    [Fact]
    public void BotStatusPage_ShouldStackTheEventsPerHourByType()
    {
        var page = Render<BotStatus>();

        page.WaitForAssertion(() => page.FindComponent<EventsPerHourChart>().ShouldNotBeNull());
        page.FindComponents<ApexPointSeries<HourlyEventCountDto>>().Select(s => s.Instance.Name)
            .ShouldBe(["Nachrichten", "Voice-Sessions", "Reaktionen", "Mitgliedschaft", "Aktivitäten"]);
        page.FindComponent<EventsPerHourChart>().Instance.Hours.ShouldAllBe(h => h.Types.Sum(t => t.Count) == h.Count);
    }

    /// <summary>Each load waits until the test completes it by its index (0 = first load).</summary>
    private sealed class PendingBotStatusClient(BotStatusDto status) : IBotStatusClient
    {
        private readonly List<TaskCompletionSource<BotStatusDto>> _loads = [];

        public int Calls
        {
            get
            {
                lock (_loads)
                {
                    return _loads.Count;
                }
            }
        }

        public Task<BotStatusDto> GetStatusAsync(CancellationToken cancellationToken)
        {
            var load = new TaskCompletionSource<BotStatusDto>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_loads)
            {
                _loads.Add(load);
            }

            return load.Task;
        }

        public void Complete(int index, BotStatusDto? result = null)
        {
            TaskCompletionSource<BotStatusDto> load;
            lock (_loads)
            {
                load = _loads[index];
            }

            load.SetResult(result ?? status);
        }
    }

    private static string Tile(IRenderedComponent<BotStatus> page, string label) => TileElement(page, label).TextContent;

    private static string TileSubtext(IRenderedComponent<BotStatus> page, string label) =>
        TileElement(page, label).QuerySelector(".apollo-stat-subtext")!.TextContent.Trim();

    private static AngleSharp.Dom.IElement TileElement(IRenderedComponent<BotStatus> page, string label) =>
        page.FindAll(".apollo-stat-tile").Single(t => t.QuerySelector(".apollo-stat-label")!.TextContent.Trim() == label);
}
