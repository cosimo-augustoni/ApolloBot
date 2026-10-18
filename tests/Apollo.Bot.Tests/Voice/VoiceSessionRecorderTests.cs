using Apollo.Bot.Status;
using Apollo.Bot.Tests.Tracking;
using Apollo.Domain.Voice;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Events;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Diagnostics;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;
using static Apollo.Bot.Tests.Voice.VoiceTestHost;

namespace Apollo.Bot.Tests.Voice;

/// <summary>
/// Recording voice state updates and the tracking hooks (#16). The reconciliation (#98) is in
/// <see cref="VoiceReconciliationTests"/>.
/// </summary>
public sealed class VoiceSessionRecorderTests(TimescaleFixture fixture) : TimescaleTest(fixture), IDisposable
{
    private static readonly TimeSpan _stay = TimeSpan.FromMinutes(25);

    private readonly TestMeterFactory _meters = new();

    [Fact]
    public async Task Join_WhenUserIsTracked_ShouldOpenOneSessionAndOneSegmentWithTheirFlags()
    {
        await using var host = await HostAsync();

        await ChangeAsync(host, VoiceOf(Alice, Lounge).Muted().Streaming());

        var (guildId, aliceId, loungeId) = (await GuildIdAsync(host), await host.UserIdAsync(Alice), await ChannelIdAsync(host, Lounge));
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

        await ChangeAsync(host, VoiceOf(Carol, Lounge));
        await ChangeAsync(host, VoiceOf(Carol, Lounge).Muted());
        await ChangeAsync(host, VoiceOf(Carol, channel: null));

        (await SessionsAsync(host)).ShouldBeEmpty();
        (await SegmentsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Leave_ShouldCloseSegmentAndSessionAsLeftNow()
    {
        await using var host = await HostAsync();
        await ChangeAsync(host, VoiceOf(Alice, Lounge));
        host.Time.Advance(_stay);

        await ChangeAsync(host, VoiceOf(Alice, channel: null));

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.EndedAt.ShouldBe(TestTime.Now + _stay);
        session.EndReason.ShouldBe(VoiceSessionEndReason.Left);
        (await SegmentsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBe(TestTime.Now + _stay);
    }

    [Fact]
    public async Task Move_ShouldCloseTheOldSessionAsMovedAndOpenOneInTheNewChannelAtTheSameInstant()
    {
        await using var host = await HostAsync();
        await ChangeAsync(host, VoiceOf(Alice, Lounge));
        host.Time.Advance(_stay);

        await ChangeAsync(host, VoiceOf(Alice, Gaming));

        var movedAt = TestTime.Now + _stay;
        var (loungeId, gamingId) = (await ChannelIdAsync(host, Lounge), await ChannelIdAsync(host, Gaming));
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
        await ChangeAsync(host, VoiceOf(Alice, Lounge));
        host.Time.Advance(_stay);
        var changed = flag switch
        {
            "mute" => VoiceOf(Alice, Lounge).Muted(),
            "deaf" => VoiceOf(Alice, Lounge).Deafened(),
            "stream" => VoiceOf(Alice, Lounge).Streaming(),
            _ => VoiceOf(Alice, Lounge).WithVideo(),
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
        await ChangeAsync(host, VoiceOf(Alice, Lounge).Muted());
        host.Time.Advance(_stay);

        // e.g. a server mute: the platform reports an update, but none of the recorded fields changed.
        await ChangeAsync(host, VoiceOf(Alice, Lounge).Muted());

        (await SegmentsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBeNull();
    }

    [Fact]
    public async Task TrackingStarted_WhenUserIsInVoice_ShouldOpenASessionImmediately()
    {
        await using var host = await HostAsync();
        host.Platform.WithVoiceState(VoiceOf(Carol, Lounge).Deafened().Build());
        host.Time.Advance(_stay);

        await host.RaiseAsync(new PlatformMemberUpdatedBuilder().WithMember(Member(Carol, Statistik)).Build());

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.UserId.ShouldBe(await host.UserIdAsync(Carol));
        session.OccurredAt.ShouldBe(TestTime.Now + _stay);
        session.EndedAt.ShouldBeNull();
        (await SegmentsAsync(host)).ShouldHaveSingleItem().SelfDeaf.ShouldBeTrue();
    }

    [Fact]
    public async Task TrackingStopped_WhenUserIsInVoice_ShouldCloseTheSessionAsTrackingStopped()
    {
        await using var host = await HostAsync();
        await ChangeAsync(host, VoiceOf(Alice, Lounge));
        host.Time.Advance(_stay);

        await host.RaiseAsync(new PlatformMemberUpdatedBuilder().WithMember(Member(Alice)).Build());

        var session = (await SessionsAsync(host)).ShouldHaveSingleItem();
        session.EndedAt.ShouldBe(TestTime.Now + _stay);
        session.EndReason.ShouldBe(VoiceSessionEndReason.TrackingStopped);
        (await SegmentsAsync(host)).ShouldHaveSingleItem().EndedAt.ShouldBe(TestTime.Now + _stay);
    }

    [Fact]
    public async Task Leave_WhenClosingFailsAndTheUserJoinsAgain_ShouldLeaveNoStaleOpenSession()
    {
        var interceptor = new VoiceDatabaseInterceptor();
        await using var host = await HostAsync(interceptor);
        await ChangeAsync(host, VoiceOf(Alice, Lounge));
        interceptor.FailNext(VoiceDatabaseInterceptor.CloseSession);
        await ChangeAsync(host, VoiceOf(Alice, channel: null));
        host.Time.Advance(_stay);

        await ChangeAsync(host, VoiceOf(Alice, Gaming));

        var gamingId = await ChannelIdAsync(host, Gaming);
        var open = (await SessionsAsync(host)).Where(s => s.EndedAt is null).ToList();
        open.ShouldHaveSingleItem().ChannelId.ShouldBe(gamingId);
        (await SegmentsAsync(host)).Count(s => s.EndedAt is null).ShouldBe(1);
    }

    [Fact]
    public async Task OpenVoiceSessionCounter_WhenNTrackedUsersAreInVoice_ShouldReportN()
    {
        await using var host = await HostAsync();

        await ChangeAsync(host, VoiceOf(Alice, Lounge));
        await ChangeAsync(host, VoiceOf(Bob, Gaming));
        await ChangeAsync(host, VoiceOf(Carol, Gaming));

        Counter(host).CountOpenSessions().ShouldBe(2);
    }

    [Fact]
    public async Task BotStatus_WhenNTrackedUsersAreInVoice_ShouldReportNOpenVoiceSessions()
    {
        await using var host = Create(Fixture, _meters, configure: services => services
            .AddSingleton<IPlatformConnectionStatus>(serviceProvider => new FakePlatformConnectionStatus(serviceProvider.GetRequiredService<TimeProvider>()))
            .AddBotStatus());
        await StartAndSyncAsync(host);

        await ChangeAsync(host, VoiceOf(Alice, Lounge));
        await ChangeAsync(host, VoiceOf(Bob, Gaming));

        host.Services.GetRequiredService<IBotStatusSource>().GetCurrent().OpenVoiceSessions.ShouldBe(2);
    }

    [Fact]
    public async Task Event_WhenStored_ShouldCountItAsRecordedWithItsEventType()
    {
        await using var host = await HostAsync();
        using var recorded = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.RecordedCounter);

        await ChangeAsync(host, VoiceOf(Alice, Lounge));
        await ChangeAsync(host, VoiceOf(Alice, channel: null));

        recorded.GetMeasurementSnapshot().Select(m => m.Tags["event_type"]).ShouldBe([nameof(PlatformVoiceStateChanged), nameof(PlatformVoiceStateChanged)]);
    }

    [Fact]
    public async Task Event_WhenTheGateDropsIt_ShouldCountItAsDroppedUntracked()
    {
        await using var host = await HostAsync();
        using var dropped = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.DroppedCounter);

        await ChangeAsync(host, VoiceOf(Carol, Lounge));
        await ChangeAsync(host, VoiceOf(Carol, channel: null));

        dropped.GetMeasurementSnapshot().Select(m => m.Tags["reason"]).ShouldBe(["untracked", "untracked"]);
    }

    public void Dispose() => _meters.Dispose();

    /// <summary>
    /// The bot after startup: the voice lifecycle service started, then the first full guild sync.
    /// </summary>
    private async Task<TrackingHost> HostAsync(IInterceptor? interceptor = null)
    {
        var host = Create(Fixture, _meters, interceptor: interceptor);
        await StartAndSyncAsync(host);
        return host;
    }

    private static IOpenVoiceSessionCounter Counter(TrackingHost host) => host.Services.GetRequiredService<IOpenVoiceSessionCounter>();
}
