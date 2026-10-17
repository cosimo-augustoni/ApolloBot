using System.Diagnostics.Metrics;
using Apollo.Bot.Status;
using Apollo.Bot.Tests.Tracking;
using Apollo.Bot.Voice;
using Apollo.Data.Voice;
using Apollo.Domain.Voice;
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

namespace Apollo.Bot.Tests.Voice;

public sealed class VoiceSessionRecorderTests(TimescaleFixture fixture) : TimescaleTest(fixture), IDisposable
{
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");

    // alice and bob hold the tracking role; carol does not.
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _bob = TestPlatform.User("bob");
    private static readonly PlatformRef _carol = TestPlatform.User("carol");

    private static readonly PlatformRef _lounge = TestPlatform.Channel("lounge");
    private static readonly PlatformRef _gaming = TestPlatform.Channel("gaming");

    private static readonly TimeSpan _stay = TimeSpan.FromMinutes(25);

    private readonly TestMeterFactory _meters = new();

    [Fact]
    public async Task Join_WhenUserIsTracked_ShouldOpenOneSessionAndOneSegmentWithTheirFlags()
    {
        await using var host = await HostAsync();

        await ChangeAsync(host, Voice(_alice, _lounge).Muted().Streaming());

        var (guildId, aliceId, loungeId) = (await GuildIdAsync(host), await host.UserIdAsync(_alice), await ChannelIdAsync(host, _lounge));
        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.ShouldSatisfyAllConditions(
            () => session.GuildId.ShouldBe(guildId),
            () => session.UserId.ShouldBe(aliceId),
            () => session.ChannelId.ShouldBe(loungeId),
            () => session.OccurredAt.ShouldBe(TestTime.Now),
            () => session.EndedAt.ShouldBeNull(),
            () => session.EndReason.ShouldBeNull());
        var segment = (await SegmentsAsync(host)).ShouldHaveSingleItem();
        segment.ShouldSatisfyAllConditions(
            () => segment.SessionId.ShouldBe(session.Id),
            () => segment.UserId.ShouldBe(aliceId),
            () => segment.OccurredAt.ShouldBe(TestTime.Now),
            () => segment.EndedAt.ShouldBeNull(),
            () => segment.SelfMute.ShouldBeTrue(),
            () => segment.SelfDeaf.ShouldBeFalse(),
            () => segment.Streaming.ShouldBeTrue(),
            () => segment.Video.ShouldBeFalse());
    }

    [Fact]
    public async Task JoinMuteLeave_WhenUserIsUntracked_ShouldStoreNothing()
    {
        await using var host = await HostAsync();

        await ChangeAsync(host, Voice(_carol, _lounge));
        await ChangeAsync(host, Voice(_carol, _lounge).Muted());
        await ChangeAsync(host, Voice(_carol, channel: null));

        (await SessionsAsync(host)).ShouldBeEmpty();
        (await SegmentsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Leave_ShouldCloseSegmentAndSessionAsLeftNow()
    {
        await using var host = await HostAsync();
        await ChangeAsync(host, Voice(_alice, _lounge));
        host.Time.Advance(_stay);

        await ChangeAsync(host, Voice(_alice, channel: null));

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.EndedAt.ShouldBe(TestTime.Now + _stay);
        session.EndReason.ShouldBe(VoiceSessionEndReason.Left);
        (await SegmentsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBe(TestTime.Now + _stay);
    }

    [Fact]
    public async Task Move_ShouldCloseTheOldSessionAsMovedAndOpenOneInTheNewChannelAtTheSameInstant()
    {
        await using var host = await HostAsync();
        await ChangeAsync(host, Voice(_alice, _lounge));
        host.Time.Advance(_stay);

        await ChangeAsync(host, Voice(_alice, _gaming));

        var movedAt = TestTime.Now + _stay;
        var (loungeId, gamingId) = (await ChannelIdAsync(host, _lounge), await ChannelIdAsync(host, _gaming));
        var sessions = await SessionsAsync(host);
        sessions.Count.ShouldBe(2);
        sessions[0].ShouldSatisfyAllConditions(
            () => sessions[0].ChannelId.ShouldBe(loungeId),
            () => sessions[0].EndedAt.ShouldBe(movedAt),
            () => sessions[0].EndReason.ShouldBe(VoiceSessionEndReason.Moved));
        sessions[1].ShouldSatisfyAllConditions(
            () => sessions[1].ChannelId.ShouldBe(gamingId),
            () => sessions[1].OccurredAt.ShouldBe(movedAt),
            () => sessions[1].EndedAt.ShouldBeNull());
        var segments = await SegmentsAsync(host);
        segments.Select(s => (s.SessionId, s.OccurredAt, s.EndedAt)).ShouldBe([
            (sessions[0].Id, TestTime.Now, movedAt),
            (sessions[1].Id, movedAt, null),
        ]);
    }

    public static TheoryData<string> FlagChanges => ["mute", "deaf", "stream", "video"];

    [Theory]
    [MemberData(nameof(FlagChanges))]
    public async Task FlagChange_ShouldCloseTheSegmentAndOpenOneWithTheNewFlagsWhileTheSessionStaysOpen(string flag)
    {
        await using var host = await HostAsync();
        await ChangeAsync(host, Voice(_alice, _lounge));
        host.Time.Advance(_stay);
        var changed = flag switch
        {
            "mute" => Voice(_alice, _lounge).Muted(),
            "deaf" => Voice(_alice, _lounge).Deafened(),
            "stream" => Voice(_alice, _lounge).Streaming(),
            _ => Voice(_alice, _lounge).WithVideo(),
        };

        await ChangeAsync(host, changed);

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.EndedAt.ShouldBeNull();
        var segments = await SegmentsAsync(host);
        segments.Count.ShouldBe(2);
        segments[0].EndedAt.ShouldBe(TestTime.Now + _stay);
        segments[1].ShouldSatisfyAllConditions(
            () => segments[1].SessionId.ShouldBe(session.Id),
            () => segments[1].OccurredAt.ShouldBe(TestTime.Now + _stay),
            () => segments[1].EndedAt.ShouldBeNull(),
            () => (segments[1].SelfMute, segments[1].SelfDeaf, segments[1].Streaming, segments[1].Video).ShouldBe(
                (flag == "mute", flag == "deaf", flag == "stream", flag == "video")));
    }

    [Fact]
    public async Task Update_WhenNothingRelevantChanged_ShouldNotCreateASegment()
    {
        await using var host = await HostAsync();
        await ChangeAsync(host, Voice(_alice, _lounge).Muted());
        host.Time.Advance(_stay);

        // e.g. a server mute: the platform reports an update, but none of the recorded fields changed.
        await ChangeAsync(host, Voice(_alice, _lounge).Muted());

        (await SegmentsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBeNull();
    }

    [Fact]
    public async Task TrackingStarted_WhenUserIsInVoice_ShouldOpenASessionImmediately()
    {
        await using var host = await HostAsync();
        host.Platform.WithVoiceState(Voice(_carol, _lounge).Deafened().Build());
        host.Time.Advance(_stay);

        await host.RaiseAsync(new PlatformMemberUpdatedBuilder().WithMember(Member(_carol, _statistik)).Build());

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.UserId.ShouldBe(await host.UserIdAsync(_carol));
        session.OccurredAt.ShouldBe(TestTime.Now + _stay);
        session.EndedAt.ShouldBeNull();
        (await SegmentsAsync(host)).ShouldHaveSingleItem().SelfDeaf.ShouldBeTrue();
    }

    [Fact]
    public async Task TrackingStopped_WhenUserIsInVoice_ShouldCloseTheSessionAsTrackingStopped()
    {
        await using var host = await HostAsync();
        await ChangeAsync(host, Voice(_alice, _lounge));
        host.Time.Advance(_stay);

        await host.RaiseAsync(new PlatformMemberUpdatedBuilder().WithMember(Member(_alice)).Build());

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.EndedAt.ShouldBe(TestTime.Now + _stay);
        session.EndReason.ShouldBe(VoiceSessionEndReason.TrackingStopped);
        (await SegmentsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBe(TestTime.Now + _stay);
    }

    [Fact]
    public async Task Startup_WhenSessionsWereLeftOpenByACrash_ShouldCloseThemAsBotRestartAndOpenNewSessionsForTrackedUsersInVoice()
    {
        await using var host = Host();
        var crashed = await SeedOpenSessionAsync(host, _alice, _gaming, TestTime.Now.AddHours(-3));
        host.Platform.WithVoiceState(Voice(_alice, _lounge).Build());

        await StartAsync(host);
        await SeedAndSyncAsync(host);

        var loungeId = await ChannelIdAsync(host, _lounge);
        var sessions = await SessionsAsync(host);
        sessions.Count.ShouldBe(2);
        sessions[0].ShouldSatisfyAllConditions(
            () => sessions[0].Id.ShouldBe(crashed.Id),
            () => sessions[0].EndedAt.ShouldBe(TestTime.Now),
            () => sessions[0].EndReason.ShouldBe(VoiceSessionEndReason.BotRestart));
        sessions[1].ShouldSatisfyAllConditions(
            () => sessions[1].ChannelId.ShouldBe(loungeId),
            () => sessions[1].OccurredAt.ShouldBe(TestTime.Now),
            () => sessions[1].EndedAt.ShouldBeNull());
        var segments = await SegmentsAsync(host);
        segments.Select(s => (s.SessionId, s.EndedAt)).ShouldBe([(crashed.Id, TestTime.Now), (sessions[1].Id, null)]);
    }

    [Fact]
    public async Task Join_WhenTheStartupCloseHasNotRunYet_ShouldCloseSessionsLeftOpenFirst()
    {
        await using var host = Host();
        var crashed = await SeedOpenSessionAsync(host, _alice, _gaming, TestTime.Now.AddHours(-3));
        await SeedAndSyncAsync(host);

        await ChangeAsync(host, Voice(_bob, _lounge));

        var sessions = await SessionsAsync(host);
        sessions.Single(s => s.Id == crashed.Id).EndReason.ShouldBe(VoiceSessionEndReason.BotRestart);
        sessions.Single(s => s.Id != crashed.Id).EndedAt.ShouldBeNull();
    }

    [Fact]
    public async Task Shutdown_ShouldCloseAllOpenSessionsAsBotRestart()
    {
        await using var host = await HostAsync();
        await ChangeAsync(host, Voice(_alice, _lounge));
        await ChangeAsync(host, Voice(_bob, _gaming));
        host.Time.Advance(_stay);

        await Lifecycle(host).StopAsync(TestContext.Current.CancellationToken);

        var sessions = await SessionsAsync(host);
        sessions.Select(s => (s.EndedAt, s.EndReason)).ShouldAllBe(s => s.EndedAt == TestTime.Now + _stay && s.EndReason == VoiceSessionEndReason.BotRestart);
        sessions.Count.ShouldBe(2);
        (await SegmentsAsync(host)).ShouldAllBe(s => s.EndedAt == TestTime.Now + _stay);
        Counter(host).CountOpenSessions().ShouldBe(0);
    }

    [Fact]
    public async Task Leave_WhenClosingFailsAndTheUserJoinsAgain_ShouldLeaveNoStaleOpenSession()
    {
        var interceptor = new FailingVoiceCloseInterceptor();
        await using var host = await HostAsync(interceptor);
        await ChangeAsync(host, Voice(_alice, _lounge));
        interceptor.Arm();
        await ChangeAsync(host, Voice(_alice, channel: null));
        host.Time.Advance(_stay);

        await ChangeAsync(host, Voice(_alice, _gaming));

        var gamingId = await ChannelIdAsync(host, _gaming);
        var open = (await SessionsAsync(host)).Where(s => s.EndedAt is null).ToList();
        open.ShouldHaveSingleItem().ChannelId.ShouldBe(gamingId);
        (await SegmentsAsync(host)).Count(s => s.EndedAt is null).ShouldBe(1);
    }

    [Fact]
    public async Task Join_AfterShutdown_ShouldOpenNothing()
    {
        await using var host = await HostAsync();
        await Lifecycle(host).StopAsync(TestContext.Current.CancellationToken);

        await ChangeAsync(host, Voice(_alice, _lounge));

        (await SessionsAsync(host)).ShouldBeEmpty();
        Counter(host).CountOpenSessions().ShouldBe(0);
    }

    [Fact]
    public async Task Resync_WhenAUserLeftWhileTheBotWasDisconnected_ShouldCloseTheirSessionAsBotRestart()
    {
        await using var host = await HostAsync();
        host.Platform.WithVoiceState(Voice(_alice, _lounge).Build());
        await ChangeAsync(host, Voice(_alice, _lounge));
        host.Time.Advance(_stay);

        host.Platform.WithVoiceState(Voice(_alice, channel: null).Build());
        await SyncAsync(host);

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.EndedAt.ShouldBe(TestTime.Now + _stay);
        session.EndReason.ShouldBe(VoiceSessionEndReason.BotRestart);
    }

    [Fact]
    public async Task OpenVoiceSessionCounter_WhenNTrackedUsersAreInVoice_ShouldReportN()
    {
        await using var host = await HostAsync();

        await ChangeAsync(host, Voice(_alice, _lounge));
        await ChangeAsync(host, Voice(_bob, _gaming));
        await ChangeAsync(host, Voice(_carol, _gaming));

        Counter(host).CountOpenSessions().ShouldBe(2);
    }

    [Fact]
    public async Task BotStatus_WhenNTrackedUsersAreInVoice_ShouldReportNOpenVoiceSessions()
    {
        await using var host = Host(services => services
            .AddSingleton<IPlatformConnectionStatus>(serviceProvider => new FakePlatformConnectionStatus(serviceProvider.GetRequiredService<TimeProvider>()))
            .AddBotStatus());
        await StartAsync(host);
        await SeedAndSyncAsync(host);

        await ChangeAsync(host, Voice(_alice, _lounge));
        await ChangeAsync(host, Voice(_bob, _gaming));

        host.Services.GetRequiredService<IBotStatusSource>().GetCurrent().OpenVoiceSessions.ShouldBe(2);
    }

    [Fact]
    public async Task Event_WhenStored_ShouldCountItAsRecordedWithItsEventType()
    {
        await using var host = await HostAsync();
        using var recorded = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.RecordedCounter);

        await ChangeAsync(host, Voice(_alice, _lounge));
        await ChangeAsync(host, Voice(_alice, channel: null));

        recorded.GetMeasurementSnapshot().Select(m => m.Tags["event_type"]).ShouldBe([nameof(PlatformVoiceStateChanged), nameof(PlatformVoiceStateChanged)]);
    }

    [Fact]
    public async Task Event_WhenTheGateDropsIt_ShouldCountItAsDroppedUntracked()
    {
        await using var host = await HostAsync();
        using var dropped = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.DroppedCounter);

        await ChangeAsync(host, Voice(_carol, _lounge));
        await ChangeAsync(host, Voice(_carol, channel: null));

        dropped.GetMeasurementSnapshot().Select(m => m.Tags["reason"]).ShouldBe(["untracked", "untracked"]);
    }

    public void Dispose() => _meters.Dispose();

    private static PlatformVoiceStateBuilder Voice(PlatformRef user, PlatformRef? channel) =>
        new PlatformVoiceStateBuilder().ForUser(user).InChannel(channel);

    /// <summary>
    /// Raises a voice state update at the host's current time, as the adapter stamps it on receipt.
    /// </summary>
    private static Task ChangeAsync(TrackingHost host, PlatformVoiceStateBuilder state) =>
        host.RaiseAsync(new PlatformVoiceStateChangedBuilder().WithState(state.Build()).At(host.Time.GetUtcNow()).Build());

    private static PlatformMember Member(PlatformRef user, params PlatformRef[] roles) =>
        new PlatformMemberBuilder().ForUser(user).WithRoles(roles).Build();

    private TrackingHost Host(Action<IServiceCollection>? configure = null, IInterceptor? interceptor = null) => new(
        Fixture,
        services =>
        {
            services.AddSingleton<IMeterFactory>(_meters);
            services.AddVoiceRecording();
            configure?.Invoke(services);
        },
        interceptor);

    /// <summary>
    /// The bot after startup: the voice lifecycle service started, then the first full guild sync.
    /// </summary>
    private async Task<TrackingHost> HostAsync(IInterceptor? interceptor = null)
    {
        var host = Host(interceptor: interceptor);
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
            .WithChannels(
                new PlatformChannelBuilder().WithRef(_lounge).Named("Lounge").OfType(PlatformChannelType.Voice).Build(),
                new PlatformChannelBuilder().WithRef(_gaming).Named("Gaming").OfType(PlatformChannelType.Voice).Build())
            .WithMembers(Member(_alice, _statistik), Member(_bob, _statistik), Member(_carol))
            .Build());

    private static IHostedService Lifecycle(TrackingHost host) =>
        host.Services.GetServices<IHostedService>().OfType<VoiceSessionLifecycleService>().Single();

    private static IOpenVoiceSessionCounter Counter(TrackingHost host) => host.Services.GetRequiredService<IOpenVoiceSessionCounter>();

    private static Task<VoiceSession> SeedOpenSessionAsync(TrackingHost host, PlatformRef user, PlatformRef channel, DateTimeOffset startedAt) =>
        host.InScopeAsync(async (db, ids) =>
        {
            var cancellationToken = TestContext.Current.CancellationToken;
            var session = new VoiceSession
            {
                GuildId = await ids.ResolveGuildAsync(TestPlatform.Guild, cancellationToken),
                OccurredAt = startedAt,
                UserId = await ids.ResolveUserAsync(user, cancellationToken),
                ChannelId = await ids.ResolveChannelAsync(TestPlatform.Guild, channel, cancellationToken),
            };
            db.VoiceSessions.Add(session);
            await db.SaveChangesAsync(cancellationToken);
            db.VoiceStateSegments.Add(new VoiceStateSegment
            {
                GuildId = session.GuildId,
                OccurredAt = startedAt,
                SessionId = session.Id,
                UserId = session.UserId,
            });
            await db.SaveChangesAsync(cancellationToken);
            return session;
        });

    private static Task<List<VoiceSession>> SessionsAsync(TrackingHost host) =>
        host.InScopeAsync((db, _) => db.VoiceSessions.AsNoTracking().OrderBy(s => s.Id).ToListAsync(TestContext.Current.CancellationToken));

    private static Task<List<VoiceStateSegment>> SegmentsAsync(TrackingHost host) =>
        host.InScopeAsync((db, _) => db.VoiceStateSegments.AsNoTracking().OrderBy(s => s.Id).ToListAsync(TestContext.Current.CancellationToken));

    private static Task<Guid> GuildIdAsync(TrackingHost host) =>
        host.InScopeAsync(async (_, ids) => (await ids.TryGetGuildAsync(TestPlatform.Guild, TestContext.Current.CancellationToken)).GetValueOrDefault());

    private static Task<Guid> ChannelIdAsync(TrackingHost host, PlatformRef channel) =>
        host.InScopeAsync(async (_, ids) => (await ids.TryGetChannelAsync(channel, TestContext.Current.CancellationToken)).GetValueOrDefault());
}
