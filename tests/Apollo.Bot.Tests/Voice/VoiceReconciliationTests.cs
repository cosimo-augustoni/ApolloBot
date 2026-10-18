using Apollo.Bot.Tests.Tracking;
using Apollo.Domain.Voice;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Diagnostics;
using Microsoft.Extensions.Logging;
using static Apollo.Bot.Tests.Voice.VoiceTestHost;

namespace Apollo.Bot.Tests.Voice;

/// <summary>
/// The voice reconciliation (#98): one rule after a time the bot could not see or store voice changes, run on startup
/// (T = the previous run's last heartbeat), after a reconnect (T = the disconnect) and periodically (T = the previous
/// run).
/// </summary>
public sealed class VoiceReconciliationTests(TimescaleFixture fixture) : TimescaleTest(fixture), IDisposable
{
    private static readonly TimeSpan _stay = TimeSpan.FromMinutes(25);

    private readonly TestMeterFactory _meters = new();

    [Fact]
    public async Task PeriodicRun_WhenALeaveFailedToSaveAndTheMemberIsOutOfVoice_ShouldCloseTheSessionAsUnobservedAtThePreviousRun()
    {
        var interceptor = new VoiceDatabaseInterceptor();
        await using var host = Create(Fixture, _meters, Interval, interceptor);
        await StartAndSyncAsync(host);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));
        await RunPeriodicallyAsync(host);
        var previousRun = host.Time.GetUtcNow();
        host.Time.Advance(TimeSpan.FromMinutes(2));
        interceptor.FailNext(VoiceDatabaseInterceptor.CloseSession);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, channel: null));

        await RunPeriodicallyAsync(host);

        (await EndsAsync(host)).ShouldBe([(previousRun, VoiceSessionEndReason.Unobserved)]);
        (await SegmentsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBe(previousRun);
        Store(host).CountOpenSessions().ShouldBe(0);
    }

    [Fact]
    public async Task PeriodicRun_WhenAJoinFailedToSave_ShouldOpenASessionFromTheCurrentVoiceState()
    {
        var interceptor = new VoiceDatabaseInterceptor();
        await using var host = Create(Fixture, _meters, Interval, interceptor);
        await StartAndSyncAsync(host);
        interceptor.FailNext(VoiceDatabaseInterceptor.OpenSession);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge).Muted());
        (await SessionsAsync(host)).ShouldBeEmpty();

        await RunPeriodicallyAsync(host);

        var loungeId = await ChannelIdAsync(host, Lounge);
        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.ShouldSatisfyAllConditions(
            () => session.ChannelId.ShouldBe(loungeId),
            () => session.OccurredAt.ShouldBe(host.Time.GetUtcNow()),
            () => session.EndedAt.ShouldBeNull());
        (await SegmentsAsync(host)).ShouldHaveSingleItem().SelfMute.ShouldBeTrue();
    }

    [Fact]
    public async Task PeriodicRun_WhenEverySessionMatchesTheVoiceStates_ShouldWriteNothing()
    {
        var interceptor = new VoiceDatabaseInterceptor();
        await using var host = Create(Fixture, _meters, Interval, interceptor);
        await StartAndSyncAsync(host);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge).Muted());
        await ChangeOnPlatformAsync(host, VoiceOf(Bob, Gaming));
        await ChangeOnPlatformAsync(host, VoiceOf(Carol, Gaming));
        var (writes, sessions, segments) = (interceptor.VoiceWrites, await SessionRowsAsync(host), await SegmentRowsAsync(host));

        await RunPeriodicallyAsync(host);

        interceptor.VoiceWrites.ShouldBe(writes);
        (await SessionRowsAsync(host)).ShouldBe(sessions);
        (await SegmentRowsAsync(host)).ShouldBe(segments);
    }

    [Fact]
    public async Task Startup_WhenTheLastHeartbeatIsWithinMaxGapAndTheMemberIsInTheSameChannel_ShouldKeepTheSameOpenRow()
    {
        await using var host = Create(Fixture, _meters);
        var open = await SeedOpenSessionAsync(host, Alice, Lounge, TestTime.Now.AddHours(-3));
        await SeedHeartbeatAsync(host, TestTime.Now - TimeSpan.FromMinutes(1));
        host.Platform.WithVoiceState(VoiceOf(Alice, Lounge).Build());

        await StartAndSyncAsync(host);

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.Id.ShouldBe(open.Id);
        session.EndedAt.ShouldBeNull();
        session.EndReason.ShouldBeNull();
        (await SegmentsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBeNull();
        Store(host).CountOpenSessions().ShouldBe(1);
    }

    [Fact]
    public async Task Startup_WhenTheMuteStateChangedDuringAShortRestart_ShouldKeepTheSessionAndStartANewSegmentAtTheStart()
    {
        await using var host = Create(Fixture, _meters);
        var open = await SeedOpenSessionAsync(host, Alice, Lounge, TestTime.Now.AddHours(-3));
        await SeedHeartbeatAsync(host, TestTime.Now - TimeSpan.FromMinutes(1));
        host.Platform.WithVoiceState(VoiceOf(Alice, Lounge).Muted().Build());

        await StartAndSyncAsync(host);

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.Id.ShouldBe(open.Id);
        session.EndedAt.ShouldBeNull();
        (await SegmentsAsync(host)).Select(s => (s.SessionId, s.OccurredAt, s.EndedAt, s.SelfMute)).ShouldBe([
            (open.Id, TestTime.Now.AddHours(-3), TestTime.Now, false),
            (open.Id, TestTime.Now, null, true),
        ]);
    }

    public static TheoryData<string> RestartMismatches => ["longer than MaxGap", "left", "switched channels"];

    [Theory]
    [MemberData(nameof(RestartMismatches))]
    public async Task Startup_WhenTheRestartWasLongOrTheMemberLeftOrSwitched_ShouldEndTheOldSessionAtTheLastHeartbeatAsUnobserved(string mismatch)
    {
        await using var host = Create(Fixture, _meters);
        var open = await SeedOpenSessionAsync(host, Alice, Lounge, TestTime.Now.AddHours(-3));
        var heartbeat = TestTime.Now - (mismatch == "longer than MaxGap" ? MaxGap + TimeSpan.FromSeconds(1) : TimeSpan.FromMinutes(1));
        await SeedHeartbeatAsync(host, heartbeat);
        PlatformRef? channel = mismatch switch
        {
            "left" => null,
            "switched channels" => Gaming,
            _ => Lounge,
        };
        host.Platform.WithVoiceState(VoiceOf(Alice, channel).Build());

        await StartAndSyncAsync(host);

        var sessions = await SessionsAsync(host);
        sessions[0].ShouldSatisfyAllConditions(
            () => sessions[0].Id.ShouldBe(open.Id),
            () => sessions[0].EndedAt.ShouldBe(heartbeat),
            () => sessions[0].EndReason.ShouldBe(VoiceSessionEndReason.Unobserved));
        if (channel is null)
        {
            sessions.Count.ShouldBe(1);
        }
        else
        {
            var channelId = await ChannelIdAsync(host, channel);
            sessions.Count.ShouldBe(2);
            sessions[1].ShouldSatisfyAllConditions(
                () => sessions[1].ChannelId.ShouldBe(channelId),
                () => sessions[1].OccurredAt.ShouldBe(TestTime.Now),
                () => sessions[1].EndedAt.ShouldBeNull());
        }

        (await SegmentsAsync(host)).First(s => s.SessionId == open.Id).EndedAt.ShouldBe(heartbeat);
    }

    [Fact]
    public async Task Startup_WhenThereIsNoHeartbeat_ShouldCloseSessionsLeftOpenAtTheStartAndOpenNewOnes()
    {
        await using var host = Create(Fixture, _meters);
        var open = await SeedOpenSessionAsync(host, Alice, Lounge, TestTime.Now.AddHours(-3));
        host.Platform.WithVoiceState(VoiceOf(Alice, Lounge).Build());

        await StartAndSyncAsync(host);

        (await EndsAsync(host)).ShouldBe([(TestTime.Now, VoiceSessionEndReason.Unobserved), (null, null)]);
        (await SessionsAsync(host))[0].Id.ShouldBe(open.Id);
    }

    [Fact]
    public async Task Startup_ShouldTakeTheLastConnectedHeartbeatAsT()
    {
        await using var host = Create(Fixture, _meters);
        await SeedOpenSessionAsync(host, Alice, Lounge, TestTime.Now.AddHours(-3));
        var lastConnected = TestTime.Now - TimeSpan.FromMinutes(10);
        await SeedHeartbeatAsync(host, lastConnected);
        await SeedHeartbeatAsync(host, TestTime.Now - TimeSpan.FromMinutes(1), connected: false);
        host.Platform.WithVoiceState(VoiceOf(Alice, Lounge).Build());

        await StartAndSyncAsync(host);

        (await EndsAsync(host)).ShouldBe([(lastConnected, VoiceSessionEndReason.Unobserved), (null, null)]);
    }

    [Fact]
    public async Task Startup_WhenTheMemberLostTrackingMeanwhile_ShouldCloseTheirSessionAtTheLastHeartbeatAsTrackingStopped()
    {
        await using var host = Create(Fixture, _meters);
        await SeedOpenSessionAsync(host, Carol, Lounge, TestTime.Now.AddHours(-3));
        var heartbeat = TestTime.Now - TimeSpan.FromMinutes(1);
        await SeedHeartbeatAsync(host, heartbeat);
        host.Platform.WithVoiceState(VoiceOf(Carol, Lounge).Build());

        await StartAndSyncAsync(host);

        (await EndsAsync(host)).ShouldBe([(heartbeat, VoiceSessionEndReason.TrackingStopped)]);
    }

    [Fact]
    public async Task Join_WhenTheTakeOverHasNotRunYet_ShouldTakeOverTheSessionsLeftOpenFirst()
    {
        await using var host = Create(Fixture, _meters);
        var open = await SeedOpenSessionAsync(host, Alice, Gaming, TestTime.Now.AddHours(-3));
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        await SyncAsync(host);

        await ChangeAsync(host, VoiceOf(Bob, Lounge));

        var sessions = await SessionsAsync(host);
        sessions.Single(s => s.Id == open.Id).EndReason.ShouldBe(VoiceSessionEndReason.Unobserved);
        sessions.Single(s => s.Id != open.Id).EndedAt.ShouldBeNull();
    }

    [Fact]
    public async Task VoiceEvent_WhenItArrivesAfterStartupBeforeTheRuleRanForTheMember_ShouldApplyTheRule()
    {
        var hold = new HeldTrackingStart(Alice);
        await using var host = Create(Fixture, _meters, hold: hold);
        var open = await SeedOpenSessionAsync(host, Alice, Lounge, TestTime.Now.AddHours(-9));
        var heartbeat = TestTime.Now.AddHours(-8);
        await SeedHeartbeatAsync(host, heartbeat);
        host.Platform.WithVoiceState(VoiceOf(Alice, Gaming).Build());
        await StartAsync(host);
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(Statistik));
        var sync = SyncAsync(host);
        await hold.Entered.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Gaming).Muted());
        hold.Release();
        await sync;

        var gamingId = await ChannelIdAsync(host, Gaming);
        (await EndsAsync(host)).ShouldBe([(heartbeat, VoiceSessionEndReason.Unobserved), (null, null)]);
        var sessions = await SessionsAsync(host);
        sessions[0].Id.ShouldBe(open.Id);
        sessions[1].ShouldSatisfyAllConditions(
            () => sessions[1].ChannelId.ShouldBe(gamingId),
            () => sessions[1].OccurredAt.ShouldBe(TestTime.Now));
        (await SegmentsAsync(host)).Last().SelfMute.ShouldBeTrue();
    }

    [Fact]
    public async Task VoiceEvent_WhenItArrivesAfterAReconnectBeforeTheSync_ShouldEndTheOldSessionAtTheDisconnect()
    {
        await using var host = Create(Fixture, _meters);
        await StartAndSyncAsync(host);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));
        host.Time.Advance(_stay);
        var disconnectedAt = host.Time.GetUtcNow();
        await ConnectionAsync(host, PlatformConnectionState.Disconnected);
        host.Time.Advance(TimeSpan.FromMinutes(5));
        await ConnectionAsync(host, PlatformConnectionState.Connected);

        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Gaming).Muted());
        await SyncAsync(host);

        (await EndsAsync(host)).ShouldBe([(disconnectedAt, VoiceSessionEndReason.Unobserved), (null, null)]);
        (await SessionsAsync(host))[1].OccurredAt.ShouldBe(host.Time.GetUtcNow());
    }

    [Fact]
    public async Task VoiceEvent_WhenTheMemberMovesAgainAfterTheRuleRanForThem_ShouldCloseAsMoved()
    {
        await using var host = Create(Fixture, _meters);
        await StartAndSyncAsync(host);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));
        host.Time.Advance(_stay);
        await ConnectionAsync(host, PlatformConnectionState.Disconnected);
        host.Time.Advance(TimeSpan.FromMinutes(5));
        await ConnectionAsync(host, PlatformConnectionState.Connected);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Gaming));
        host.Time.Advance(TimeSpan.FromSeconds(10));
        var movedAt = host.Time.GetUtcNow();

        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));

        (await EndsAsync(host)).Skip(1).ShouldBe([(movedAt, VoiceSessionEndReason.Moved), (null, null)]);
    }

    [Fact]
    public async Task Reconnect_WhenTheDisconnectWasLongerThanMaxGap_ShouldEndTheSessionAtTheDisconnectAndOpenANewOne()
    {
        await using var host = Create(Fixture, _meters);
        await StartAndSyncAsync(host);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));
        host.Time.Advance(_stay);
        var disconnectedAt = host.Time.GetUtcNow();
        await ConnectionAsync(host, PlatformConnectionState.Disconnected);
        host.Time.Advance(TimeSpan.FromMinutes(5));

        await ConnectionAsync(host, PlatformConnectionState.Connected);
        await SyncAsync(host);

        (await EndsAsync(host)).ShouldBe([(disconnectedAt, VoiceSessionEndReason.Unobserved), (null, null)]);
        (await SessionsAsync(host))[1].OccurredAt.ShouldBe(host.Time.GetUtcNow());
    }

    [Fact]
    public async Task Reconnect_WhenTheDisconnectWasShortAndTheMemberStayed_ShouldKeepTheSession()
    {
        await using var host = Create(Fixture, _meters);
        await StartAndSyncAsync(host);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));
        host.Time.Advance(_stay);
        await ConnectionAsync(host, PlatformConnectionState.Disconnected);
        host.Time.Advance(TimeSpan.FromMinutes(1));

        await ConnectionAsync(host, PlatformConnectionState.Connected);
        await SyncAsync(host);

        (await EndsAsync(host)).ShouldBe([(null, null)]);
    }

    [Fact]
    public async Task Reconnect_WhenTheMemberLeftWhileDisconnected_ShouldEndTheSessionAtTheDisconnectAsUnobserved()
    {
        await using var host = Create(Fixture, _meters);
        await StartAndSyncAsync(host);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));
        host.Time.Advance(_stay);
        var disconnectedAt = host.Time.GetUtcNow();
        await ConnectionAsync(host, PlatformConnectionState.Disconnected);
        host.Time.Advance(TimeSpan.FromMinutes(1));
        host.Platform.WithVoiceState(VoiceOf(Alice, channel: null).Build());

        await ConnectionAsync(host, PlatformConnectionState.Connected);
        await SyncAsync(host);

        (await EndsAsync(host)).ShouldBe([(disconnectedAt, VoiceSessionEndReason.Unobserved)]);
    }

    [Fact]
    public async Task PeriodicRun_WhileTheGatewayIsDisconnected_ShouldWaitForTheReconnect()
    {
        await using var host = Create(Fixture, _meters, Interval);
        await StartAndSyncAsync(host);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));
        await ConnectionAsync(host, PlatformConnectionState.Disconnected);
        host.Platform.WithVoiceState(VoiceOf(Alice, channel: null).Build());

        host.Time.Advance(Interval);
        await TrackingHost.WaitUntilAsync(
            () => host.Logs.GetSnapshot().Any(log => log.Message.Contains("gateway is disconnected", StringComparison.Ordinal)),
            "the periodic run was skipped");

        (await EndsAsync(host)).ShouldBe([(null, null)]);
    }

    [Fact]
    public async Task PeriodicRun_AfterAReconnectBeforeItsSync_ShouldWriteNothing()
    {
        await using var host = Create(Fixture, _meters, Interval);
        await StartAndSyncAsync(host);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));
        host.Time.Advance(Interval - TimeSpan.FromSeconds(3));
        await ConnectionAsync(host, PlatformConnectionState.Disconnected);
        host.Time.Advance(TimeSpan.FromSeconds(2));
        await ConnectionAsync(host, PlatformConnectionState.Connected);

        // The new session's cache does not have the voice states yet; the tick is due a second later.
        host.Platform.WithVoiceState(VoiceOf(Alice, channel: null).Build());
        await SkippedPeriodicRunAsync(host, "the reconnect's sync reconciles", TimeSpan.FromSeconds(1));
        host.Platform.WithVoiceState(VoiceOf(Alice, Lounge).Build());
        await SyncAsync(host);

        (await EndsAsync(host)).ShouldBe([(null, null)]);
    }

    [Fact]
    public async Task PeriodicRun_WhileTheGuildIsUnavailable_ShouldWriteNothing()
    {
        await using var host = Create(Fixture, _meters, Interval);
        await StartAndSyncAsync(host);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));
        host.Platform.WithGuildAvailability(false);

        await SkippedPeriodicRunAsync(host, "the guild is unavailable");

        (await EndsAsync(host)).ShouldBe([(null, null)]);
        (await SegmentsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Shutdown_ShouldLeaveTheOpenSessionsOpen()
    {
        await using var host = Create(Fixture, _meters);
        await StartAndSyncAsync(host);
        await ChangeAsync(host, VoiceOf(Alice, Lounge));
        await ChangeAsync(host, VoiceOf(Bob, Gaming));
        host.Time.Advance(_stay);

        await Lifecycle(host).StopAsync(TestContext.Current.CancellationToken);

        (await EndsAsync(host)).ShouldBe([(null, null), (null, null)]);
        (await SegmentsAsync(host)).ShouldAllBe(s => s.EndedAt == null);
    }

    [Fact]
    public async Task PeriodicRun_WhenTheDatabaseIsUnreachable_ShouldLogAndRetryOnTheNextTickWhileTheRecorderKeepsWorking()
    {
        var interceptor = new VoiceDatabaseInterceptor();
        await using var host = Create(Fixture, _meters, Interval, interceptor);
        await StartAndSyncAsync(host);
        var startupRun = host.Time.GetUtcNow();
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));
        interceptor.GoDown();
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, channel: null));

        host.Time.Advance(Interval);
        await TrackingHost.WaitUntilAsync(
            () => host.Logs.GetSnapshot().Any(log => log.Level == LogLevel.Error && log.Message.Contains("periodic voice reconciliation failed", StringComparison.Ordinal)),
            "the failed periodic run was logged");
        interceptor.ComeBack();
        await ChangeOnPlatformAsync(host, VoiceOf(Bob, Gaming));
        await RunPeriodicallyAsync(host);

        var bobId = await host.UserIdAsync(Bob);
        var sessions = await SessionsAsync(host);
        sessions.Select(s => (s.EndedAt, s.EndReason)).ShouldBe([(startupRun, VoiceSessionEndReason.Unobserved), (null, null)]);
        sessions[1].UserId.ShouldBe(bobId);
    }

    [Fact]
    public async Task VoiceEvent_WhenItArrivesWhileAReconciliationRuns_ShouldWin()
    {
        await using var host = Create(Fixture, _meters, Interval);
        await StartAndSyncAsync(host);
        await ChangeOnPlatformAsync(host, VoiceOf(Alice, Lounge));
        var aliceId = await host.UserIdAsync(Alice);
        host.Platform.HoldVoiceStateListings();
        host.Time.Advance(Interval);
        await host.Platform.VoiceStateListingHeld.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var leftAt = host.Time.GetUtcNow();

        var leave = ChangeOnPlatformAsync(host, VoiceOf(Alice, channel: null));
        await TrackingHost.WaitUntilAsync(() => Store(host).HasPendingEvents(aliceId), "the leave is on its way");
        host.Platform.ReleaseVoiceStateListings();
        await leave;
        await TrackingHost.WaitUntilAsync(() => Store(host).LastReconciledAt == leftAt, "the periodic run finished");

        (await EndsAsync(host)).ShouldBe([(leftAt, VoiceSessionEndReason.Left)]);
        Store(host).CountOpenSessions().ShouldBe(0);
    }

    public void Dispose() => _meters.Dispose();

    /// <summary>
    /// Advances the clock by <paramref name="advance"/> (default: the interval) and waits until the periodic run either logged that it skipped for
    /// <paramref name="reason"/> or ran, so the test's assertions decide which.
    /// </summary>
    private static async Task SkippedPeriodicRunAsync(TrackingHost host, string reason, TimeSpan? advance = null)
    {
        int Skips() => host.Logs.GetSnapshot().Count(log => log.Message.Contains(reason, StringComparison.Ordinal));
        var (skips, reconciledAt) = (Skips(), Store(host).LastReconciledAt);
        host.Time.Advance(advance ?? Interval);
        await TrackingHost.WaitUntilAsync(
            () => Skips() > skips || Store(host).LastReconciledAt != reconciledAt,
            "the periodic run skipped or ran");
    }

    private static async Task<List<string>> SessionRowsAsync(TrackingHost host) =>
        [.. (await SessionsAsync(host)).Select(s => $"{s.Id} {s.UserId} {s.ChannelId} {s.OccurredAt:O} {s.EndedAt:O} {s.EndReason}")];

    private static async Task<List<string>> SegmentRowsAsync(TrackingHost host) =>
        [.. (await SegmentsAsync(host)).Select(s => $"{s.Id} {s.SessionId} {s.OccurredAt:O} {s.EndedAt:O} {s.SelfMute} {s.SelfDeaf} {s.Streaming} {s.Video}")];
}
