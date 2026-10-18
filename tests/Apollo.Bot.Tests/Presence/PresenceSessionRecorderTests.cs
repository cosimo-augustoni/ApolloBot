using System.Diagnostics.Metrics;
using Apollo.Bot.Presence;
using Apollo.Bot.Reconciliation;
using Apollo.Bot.Tests.Tracking;
using Apollo.Data.Presence;
using Apollo.Domain.Presence;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Diagnostics;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using Microsoft.Extensions.Hosting;

namespace Apollo.Bot.Tests.Presence;

public sealed class PresenceSessionRecorderTests(TimescaleFixture fixture) : TimescaleTest(fixture), IDisposable
{
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");

    // alice and bob hold the tracking role; carol does not.
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _bob = TestPlatform.User("bob");
    private static readonly PlatformRef _carol = TestPlatform.User("carol");

    private static readonly TimeSpan _session = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(5);

    private readonly TestMeterFactory _meters = new();

    [Fact]
    public async Task Update_WhenTrackedUserPlaysForFiveMinutes_ShouldStoreOneSessionWithItsSpan()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        host.Time.Advance(_session);

        await UpdateAsync(host, Presence(_alice));

        var (guildId, aliceId) = (await GuildIdAsync(host), await host.UserIdAsync(_alice));
        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.ShouldSatisfyAllConditions(
            () => session.GuildId.ShouldBe(guildId),
            () => session.UserId.ShouldBe(aliceId),
            () => session.ActivityType.ShouldBe(PresenceActivityType.Playing),
            () => session.ActivityName.ShouldBe("X"),
            () => session.OccurredAt.ShouldBe(TestTime.Now),
            () => session.EndedAt.ShouldBe(TestTime.Now + _session));
    }

    [Fact]
    public async Task Update_WhenTheActivityLasted59Seconds_ShouldStoreNothing()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        host.Time.Advance(TimeSpan.FromSeconds(59));

        await UpdateAsync(host, Presence(_alice));

        (await SessionsAsync(host)).ShouldBeEmpty();
        Store(host).Machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public async Task Update_WhenTheActivityLastedExactlyOneMinute_ShouldStoreIt()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        host.Time.Advance(TimeSpan.FromMinutes(1));

        await UpdateAsync(host, Presence(_alice));

        (await SessionsAsync(host)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Update_WhenTrackedUserHasTwoSimultaneousActivities_ShouldStoreTwoIndependentSessions()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        host.Time.Advance(TimeSpan.FromMinutes(2));
        await UpdateAsync(host, Presence(_alice).Playing("X").ListeningTo("Spotify"));
        host.Time.Advance(TimeSpan.FromMinutes(3));
        await UpdateAsync(host, Presence(_alice).ListeningTo("Spotify"));
        host.Time.Advance(TimeSpan.FromMinutes(4));

        await UpdateAsync(host, Presence(_alice));

        (await SessionsAsync(host)).Select(s => (s.ActivityType, s.ActivityName, s.OccurredAt, s.EndedAt)).ShouldBe([
            (PresenceActivityType.Playing, "X", TestTime.Now, TestTime.Now.AddMinutes(5)),
            (PresenceActivityType.Listening, "Spotify", TestTime.Now.AddMinutes(2), TestTime.Now.AddMinutes(9)),
        ]);
    }

    [Fact]
    public async Task Update_WhenTrackedUserSwitchesGames_ShouldCloseTheOldSessionAndOpenTheNewOneAtTheSameInstant()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        host.Time.Advance(_session);
        await UpdateAsync(host, Presence(_alice).Playing("Y"));
        host.Time.Advance(_session);

        await UpdateAsync(host, Presence(_alice));

        var switchedAt = TestTime.Now + _session;
        (await SessionsAsync(host)).Select(s => (s.ActivityName, s.OccurredAt, s.EndedAt)).ShouldBe([
            ("X", TestTime.Now, switchedAt),
            ("Y", switchedAt, switchedAt + _session),
        ]);
    }

    [Fact]
    public async Task Update_WhenTrackedUserGoesOffline_ShouldCloseAllTheirSessions()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).Playing("X").ListeningTo("Spotify"));
        host.Time.Advance(_session);

        // Discord reports offline without activities; an offline status alone closes everything too.
        await UpdateAsync(host, Presence(_alice).WithStatus(PlatformPresenceStatus.Offline).Playing("X"));

        (await SessionsAsync(host)).Select(s => s.ActivityName).ShouldBe(["X", "Spotify"], ignoreOrder: true);
        Store(host).Machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public async Task Update_WhenOnlyTheStatusChanges_ShouldKeepTheSessionOpen()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        host.Time.Advance(_session);

        await UpdateAsync(host, Presence(_alice).WithStatus(PlatformPresenceStatus.Idle).Playing("X"));

        (await SessionsAsync(host)).ShouldBeEmpty();
        Store(host).Machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public async Task Update_WhenUserIsUntracked_ShouldStoreNothingAndHoldNoSession()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_carol).Playing("X"));

        Store(host).Machine.OpenSessionCount.ShouldBe(0);
        host.Time.Advance(_session);
        await UpdateAsync(host, Presence(_carol));
        (await SessionsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Update_WhenTheActivityIsACustomStatus_ShouldIgnoreIt()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).WithActivity(PlatformActivityType.Custom, "Custom Status"));
        host.Time.Advance(_session);

        await UpdateAsync(host, Presence(_alice));

        (await SessionsAsync(host)).ShouldBeEmpty();
        Store(host).Machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public async Task Update_WhenThePlatformHasNoPresenceActivities_ShouldDoNothing()
    {
        await using var host = await HostAsync();
        host.Platform.WithCapabilities(new PlatformCapabilities(PresenceActivities: false));
        using var dropped = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.DroppedCounter);

        await UpdateAsync(host, Presence(_alice).Playing("X"));
        await UpdateAsync(host, Presence(_carol).Playing("X"));

        Store(host).Machine.OpenSessionCount.ShouldBe(0);
        dropped.GetMeasurementSnapshot().ShouldBeEmpty();
    }

    [Fact]
    public async Task TrackingStarted_WhenUserIsAlreadyPlaying_ShouldOpenASessionImmediately()
    {
        await using var host = await HostAsync();
        host.Platform.WithPresence(Presence(_carol).Playing("X").Build());
        host.Time.Advance(_session);
        await host.RaiseAsync(new PlatformMemberUpdatedBuilder().WithMember(Member(_carol, _statistik)).Build());
        host.Time.Advance(_session);

        await UpdateAsync(host, Presence(_carol));

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.UserId.ShouldBe(await host.UserIdAsync(_carol));
        session.OccurredAt.ShouldBe(TestTime.Now + _session);
        session.EndedAt.ShouldBe(TestTime.Now + _session + _session);
    }

    [Fact]
    public async Task TrackingStopped_WhenTheSessionLastedAMinute_ShouldCloseAndStoreIt()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        await UpdateAsync(host, Presence(_bob).Playing("Y"));
        host.Time.Advance(_session);

        await host.RaiseAsync(new PlatformMemberUpdatedBuilder().WithMember(Member(_alice)).Build());

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.ActivityName.ShouldBe("X");
        session.EndedAt.ShouldBe(TestTime.Now + _session);
        Store(host).Machine.Users.ShouldBe([await host.UserIdAsync(_bob)]);
    }

    [Fact]
    public async Task TrackingStopped_WhenTheSessionWasShorterThanAMinute_ShouldStoreNothing()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        host.Time.Advance(TimeSpan.FromSeconds(30));

        await host.RaiseAsync(new PlatformMemberUpdatedBuilder().WithMember(Member(_alice)).Build());

        (await SessionsAsync(host)).ShouldBeEmpty();
        Store(host).Machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public async Task Startup_WhenTrackedUsersArePlaying_ShouldOpenTheirSessionsAfterTheFirstSync()
    {
        await using var host = Host();
        host.Platform.WithPresence(Presence(_alice).Playing("X").Build());
        host.Platform.WithPresence(Presence(_carol).Playing("Y").Build());

        await StartAsync(host);
        await SeedAndSyncAsync(host);

        Store(host).Machine.Users.ShouldBe([await host.UserIdAsync(_alice)]);
        host.Time.Advance(_session);
        await UpdateAsync(host, Presence(_alice));
        (await SessionsAsync(host)).ShouldHaveSingleItem().OccurredAt.ShouldBe(TestTime.Now);
    }

    [Fact]
    public async Task Shutdown_ShouldStoreTheOpenSessionsOfAtLeastAMinute()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        host.Time.Advance(_session);
        await UpdateAsync(host, Presence(_bob).Playing("Y"));
        host.Time.Advance(TimeSpan.FromSeconds(30));

        await Lifecycle(host).StopAsync(TestContext.Current.CancellationToken);

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.ActivityName.ShouldBe("X");
        session.EndedAt.ShouldBe(TestTime.Now + _session + TimeSpan.FromSeconds(30));
        Store(host).Machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public async Task Update_AfterShutdown_ShouldOpenNothing()
    {
        await using var host = await HostAsync();
        await Lifecycle(host).StopAsync(TestContext.Current.CancellationToken);

        await UpdateAsync(host, Presence(_alice).Playing("X"));

        Store(host).Machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public async Task Resync_WhenAUserStoppedPlayingWhileTheBotWasDisconnected_ShouldCloseTheirSession()
    {
        await using var host = await HostAsync();
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        host.Platform.WithPresence(Presence(_alice).Playing("Y").Build());
        host.Time.Advance(_session);

        await SyncAsync(host);

        (await SessionsAsync(host)).ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            s => s.ActivityName.ShouldBe("X"),
            s => s.EndedAt.ShouldBe(TestTime.Now + _session));
        Store(host).Machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public async Task Update_WhenStoringFails_ShouldKeepTheSessionOpenForTheNextClose()
    {
        var interceptor = new FailingPresenceInsertInterceptor();
        await using var host = await HostAsync(interceptor);
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        host.Time.Advance(_session);
        interceptor.Arm();
        await UpdateAsync(host, Presence(_alice));
        (await SessionsAsync(host)).ShouldBeEmpty();
        host.Time.Advance(_session);

        await UpdateAsync(host, Presence(_alice));

        (await SessionsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBe(TestTime.Now + _session + _session);
    }

    [Fact]
    public async Task Event_WhenStored_ShouldCountItAsRecordedWithItsEventType()
    {
        await using var host = await HostAsync();
        using var recorded = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.RecordedCounter);
        await UpdateAsync(host, Presence(_alice).Playing("X"));
        host.Time.Advance(_session);

        await UpdateAsync(host, Presence(_alice));

        recorded.GetMeasurementSnapshot().Select(m => m.Tags["event_type"]).ShouldBe([nameof(PlatformPresenceUpdated)]);
    }

    [Fact]
    public async Task Event_WhenTheGateDropsIt_ShouldCountItAsDroppedUntracked()
    {
        await using var host = await HostAsync();
        using var dropped = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.DroppedCounter);

        await UpdateAsync(host, Presence(_carol).Playing("X"));
        await UpdateAsync(host, Presence(_carol));

        dropped.GetMeasurementSnapshot().Select(m => m.Tags["reason"]).ShouldBe(["untracked", "untracked"]);
    }

    [Fact]
    public async Task PeriodicRun_WhenStoringAClosedSessionFailedAndTheActivityIsGone_ShouldStoreItEndingAtThePreviousRun()
    {
        var interceptor = new FailingPresenceInsertInterceptor();
        await using var host = await HostAsync(interceptor, _interval);
        await UpdateOnPlatformAsync(host, Presence(_alice).Playing("X"));
        await RunPeriodicallyAsync(host);
        var previousRun = host.Time.GetUtcNow();
        host.Time.Advance(TimeSpan.FromMinutes(2));
        interceptor.Arm();
        await UpdateOnPlatformAsync(host, Presence(_alice));
        (await SessionsAsync(host)).ShouldBeEmpty();

        await RunPeriodicallyAsync(host);

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.OccurredAt.ShouldBe(TestTime.Now);
        session.EndedAt.ShouldBe(previousRun);
        Store(host).Machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public async Task PeriodicRun_WhenAnActivityHasNoSession_ShouldOpenOneFromNow()
    {
        await using var host = await HostAsync(interval: _interval);
        host.Platform.WithPresence(Presence(_alice).Playing("X").Build());

        await RunPeriodicallyAsync(host);
        var openedAt = host.Time.GetUtcNow();
        host.Time.Advance(TimeSpan.FromMinutes(2));
        await UpdateOnPlatformAsync(host, Presence(_alice));

        (await SessionsAsync(host)).ShouldHaveSingleItem().OccurredAt.ShouldBe(openedAt);
    }

    [Fact]
    public async Task PeriodicRun_WhenEverySessionMatchesThePresences_ShouldWriteNothingAndKeepTheStarts()
    {
        var interceptor = new FailingPresenceInsertInterceptor();
        await using var host = await HostAsync(interceptor, _interval);
        await UpdateOnPlatformAsync(host, Presence(_alice).Playing("X"));
        await UpdateOnPlatformAsync(host, Presence(_bob).ListeningTo("Spotify"));

        await RunPeriodicallyAsync(host);

        interceptor.Inserts.ShouldBe(0);
        await UpdateOnPlatformAsync(host, Presence(_alice));
        (await SessionsAsync(host)).ShouldHaveSingleItem().OccurredAt.ShouldBe(TestTime.Now);
    }

    [Fact]
    public async Task PeriodicRun_WhenAPresenceUpdateIsOnItsWay_ShouldLeaveTheMemberToTheUpdate()
    {
        await using var host = await HostAsync(interval: _interval);
        await UpdateOnPlatformAsync(host, Presence(_alice).Playing("X"));
        var aliceId = await host.UserIdAsync(_alice);
        host.Time.Advance(_interval - TimeSpan.FromSeconds(1));
        host.Platform.HoldStateListings(snapshotOnRelease: true);
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.Platform.StateListingHeld.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var stoppedAt = host.Time.GetUtcNow();

        var update = UpdateOnPlatformAsync(host, Presence(_alice));
        await TrackingHost.WaitUntilAsync(() => Store(host).HasPendingEvents(aliceId), "the update is on its way");
        host.Platform.ReleaseStateListings();
        await update;
        await TrackingHost.WaitUntilAsync(() => Store(host).LastReconciledAt == stoppedAt, "the periodic run finished");

        (await SessionsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBe(stoppedAt);
    }

    [Fact]
    public async Task PeriodicRun_WhileTheGuildIsUnavailable_ShouldCloseNothing()
    {
        await using var host = await HostAsync(interval: _interval);
        await UpdateOnPlatformAsync(host, Presence(_alice).Playing("X"));
        host.Platform.WithGuildAvailability(false);

        await SkippedOrRanAsync(host);

        Store(host).Machine.OpenSessionCount.ShouldBe(1);
        (await SessionsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task PeriodicRun_AfterAReconnectBeforeItsSync_ShouldCloseNothing()
    {
        await using var host = await HostAsync(interval: _interval);
        await UpdateOnPlatformAsync(host, Presence(_alice).Playing("X"));
        await ConnectionAsync(host, PlatformConnectionState.Disconnected);
        await ConnectionAsync(host, PlatformConnectionState.Connected);
        host.Platform.WithPresence(Presence(_alice).Build());

        await SkippedOrRanAsync(host);

        Store(host).Machine.OpenSessionCount.ShouldBe(1);
        (await SessionsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task PeriodicRun_WhenTheDatabaseFailsAtATick_ShouldLogAndRetryOnTheNextTick()
    {
        var interceptor = new FailingPresenceInsertInterceptor();
        await using var host = await HostAsync(interceptor, _interval);
        await UpdateOnPlatformAsync(host, Presence(_alice).Playing("X"));
        await RunPeriodicallyAsync(host);
        var lastSuccessfulRun = host.Time.GetUtcNow();
        host.Time.Advance(TimeSpan.FromMinutes(2));
        interceptor.Arm();
        await UpdateOnPlatformAsync(host, Presence(_alice));
        interceptor.Arm();

        host.Time.Advance(TimeSpan.FromMinutes(3));
        await TrackingHost.WaitUntilAsync(
            () => host.Logs.GetSnapshot().Any(log => log.Message.Contains("periodic presence reconciliation failed", StringComparison.Ordinal)),
            "the failed periodic run was logged");
        await RunPeriodicallyAsync(host);

        (await SessionsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBe(lastSuccessfulRun);
    }

    public void Dispose() => _meters.Dispose();

    /// <summary>
    /// Advances the fake clock by the interval and waits until that periodic run finished.
    /// </summary>
    private static async Task RunPeriodicallyAsync(TrackingHost host)
    {
        var before = Store(host).LastReconciledAt;
        host.Time.Advance(_interval);
        await TrackingHost.WaitUntilAsync(() => Store(host).LastReconciledAt != before, "the periodic presence reconciliation ran");
    }

    /// <summary>
    /// Advances the fake clock by the interval and waits until the periodic run either logged a skip or ran, so the
    /// test's assertions decide which.
    /// </summary>
    private static async Task SkippedOrRanAsync(TrackingHost host)
    {
        int Skips() => host.Logs.GetSnapshot().Count(log => log.Message.Contains("Skipped the periodic presence reconciliation", StringComparison.Ordinal));
        var (skips, before) = (Skips(), Store(host).LastReconciledAt);
        host.Time.Advance(_interval);
        await TrackingHost.WaitUntilAsync(() => Skips() > skips || Store(host).LastReconciledAt != before, "the periodic run skipped or ran");
    }

    /// <summary>
    /// The presence changes on the platform (its cache, which the reconciliation reads) and the update is raised.
    /// </summary>
    private static Task UpdateOnPlatformAsync(TrackingHost host, PlatformPresenceBuilder presence)
    {
        host.Platform.WithPresence(presence.Build());
        return UpdateAsync(host, presence);
    }

    private static Task ConnectionAsync(TrackingHost host, PlatformConnectionState state) =>
        host.RaiseAsync(new PlatformConnectionStateChangedBuilder().WithState(state).At(host.Time.GetUtcNow()).Build());

    private static PlatformPresenceBuilder Presence(PlatformRef user) => new PlatformPresenceBuilder().ForUser(user);

    /// <summary>
    /// Raises a presence update at the host's current time, as the adapter stamps it on receipt.
    /// </summary>
    private static Task UpdateAsync(TrackingHost host, PlatformPresenceBuilder presence) =>
        host.RaiseAsync(new PlatformPresenceUpdatedBuilder().WithPresence(presence.Build()).At(host.Time.GetUtcNow()).Build());

    private static PlatformMember Member(PlatformRef user, params PlatformRef[] roles) =>
        new PlatformMemberBuilder().ForUser(user).WithRoles(roles).Build();

    /// <summary>
    /// A host with the presence recorder. Without <paramref name="interval"/> the periodic run is due only after a day.
    /// </summary>
    private TrackingHost Host(IInterceptor? interceptor = null, TimeSpan? interval = null) => new(
        Fixture,
        services =>
        {
            services.AddSingleton<IMeterFactory>(_meters);
            services.AddPresenceRecording();
            services.Configure<ReconciliationOptions>(options => options.Interval = interval ?? TimeSpan.FromDays(1));
        },
        interceptor);

    /// <summary>
    /// The bot after startup: the presence lifecycle service started, then the first full guild sync.
    /// </summary>
    private async Task<TrackingHost> HostAsync(IInterceptor? interceptor = null, TimeSpan? interval = null)
    {
        var host = Host(interceptor, interval);
        await StartAsync(host);
        await SeedAndSyncAsync(host);
        return host;
    }

    private static Task StartAsync(TrackingHost host) => Lifecycle(host).StartAsync(TestContext.Current.CancellationToken);

    private static async Task SeedAndSyncAsync(TrackingHost host)
    {
        await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));
        await SyncAsync(host);
    }

    /// <summary>
    /// A full guild sync, as on startup and after every reconnect.
    /// </summary>
    private static Task SyncAsync(TrackingHost host) =>
        host.SyncAsync(new PlatformGuildAvailableBuilder()
            .WithRoles(new PlatformRoleBuilder().WithRef(_statistik).Named("Statistik").Build())
            .WithMembers(Member(_alice, _statistik), Member(_bob, _statistik), Member(_carol))
            .Build());

    private static IHostedService Lifecycle(TrackingHost host) =>
        host.Services.GetServices<IHostedService>().OfType<PresenceSessionLifecycleService>().Single();

    private static PresenceSessionStore Store(TrackingHost host) => host.Services.GetRequiredService<PresenceSessionStore>();

    private static Task<List<PresenceActivitySession>> SessionsAsync(TrackingHost host) =>
        host.InScopeAsync((db, _) => db.PresenceActivitySessions.AsNoTracking().OrderBy(s => s.Id).ToListAsync(TestContext.Current.CancellationToken));

    private static Task<Guid> GuildIdAsync(TrackingHost host) =>
        host.InScopeAsync(async (_, ids) => (await ids.TryGetGuildAsync(TestPlatform.Guild, TestContext.Current.CancellationToken)).GetValueOrDefault());
}
