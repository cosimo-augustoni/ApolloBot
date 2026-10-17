using System.Diagnostics.Metrics;
using Apollo.Bot.Membership;
using Apollo.Bot.Tests.Tracking;
using Apollo.Data.Membership;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Diagnostics;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Apollo.Bot.Tests.Membership;

public sealed class MembershipRecorderTests(TimescaleFixture fixture) : TimescaleTest(fixture), IDisposable
{
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");
    private static readonly PlatformRef _admin = TestPlatform.Role("admin");

    // alice holds the tracking role; carol does not; dave is no member yet.
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _carol = TestPlatform.User("carol");
    private static readonly PlatformRef _dave = TestPlatform.User("dave");

    private readonly TestMeterFactory _meters = new();

    public enum HandlerOrder
    {
        AfterGuildStateSync,
        BeforeGuildStateSync,
    }

    [Theory]
    [InlineData(HandlerOrder.AfterGuildStateSync)]
    [InlineData(HandlerOrder.BeforeGuildStateSync)]
    public async Task Leave_WhenTheMemberIsTracked_ShouldStoreOneLeaveRowEvenThoughTheLeaveStopsTracking(HandlerOrder order)
    {
        await using var host = await HostAsync(order);

        await host.RaiseAsync(Left(_alice).Build());

        var row = (await EventsAsync(host)).ShouldHaveSingleItem();
        var (guildId, aliceId) = (await GuildIdAsync(host), await host.UserIdAsync(_alice));
        row.ShouldSatisfyAllConditions(
            () => row.GuildId.ShouldBe(guildId),
            () => row.UserId.ShouldBe(aliceId),
            () => row.OccurredAt.ShouldBe(TestTime.Now),
            () => row.Kind.ShouldBe(MembershipEventKind.Leave));
        host.Gate.TryGetTrackedUserId(_alice).ShouldBeNull();
    }

    [Fact]
    public async Task Leave_WhenTheMemberIsUntracked_ShouldStoreNothing()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Left(_carol).Build());

        (await EventsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Leave_WhenTheMemberStoppedBeingTrackedBefore_ShouldStoreNothing()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(new PlatformMemberUpdatedBuilder().WithMember(Member(_alice)).Build());
        host.Time.Advance(TimeSpan.FromMinutes(5));

        await host.RaiseAsync(Left(_alice).At(host.Time.GetUtcNow()).Build());

        (await EventsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Leave_WhenTheUserWasNeverSeen_ShouldStoreNothingAndCreateNoUserRow()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Left(_dave).Build());

        (await EventsAsync(host)).ShouldBeEmpty();
        (await host.InScopeAsync((_, ids) => ids.TryGetUserAsync(_dave, TestContext.Current.CancellationToken))).ShouldBeNull();
    }

    [Theory]
    [InlineData(HandlerOrder.AfterGuildStateSync)]
    [InlineData(HandlerOrder.BeforeGuildStateSync)]
    public async Task Ban_WhenTheMemberIsTrackedAndTheBanComesFirst_ShouldStoreOneBanAndOneLeaveRow(HandlerOrder order)
    {
        await using var host = await HostAsync(order);

        await host.RaiseAsync(Banned(_alice).Build());
        await host.RaiseAsync(Left(_alice).Build());

        (await KindsAsync(host)).ShouldBe([MembershipEventKind.Ban, MembershipEventKind.Leave]);
    }

    /// <summary>
    /// Discord sends the ban and the member remove as two events in either order, each stamped on receipt; when the
    /// remove comes first, it has already closed the tracking period when the ban arrives.
    /// </summary>
    [Fact]
    public async Task Ban_WhenTheMemberRemoveCameMomentsBefore_ShouldStillStoreTheBanRow()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Left(_alice).Build());
        host.Time.Advance(TimeSpan.FromSeconds(2));

        await host.RaiseAsync(Banned(_alice).At(host.Time.GetUtcNow()).Build());

        (await KindsAsync(host)).ShouldBe([MembershipEventKind.Leave, MembershipEventKind.Ban]);
    }

    [Fact]
    public async Task Ban_WhenTheUserLeftLongBefore_ShouldStoreNothingForTheBan()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Left(_alice).Build());
        host.Time.Advance(TimeSpan.FromMinutes(5));

        await host.RaiseAsync(Banned(_alice).At(host.Time.GetUtcNow()).Build());

        (await KindsAsync(host)).ShouldBe([MembershipEventKind.Leave]);
    }

    [Fact]
    public async Task Ban_WhenTheMemberIsUntracked_ShouldStoreNothing()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Banned(_carol).Build());
        await host.RaiseAsync(Left(_carol).Build());

        (await EventsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Join_WhenTheTrackingRoleIsAlreadySynced_ShouldStoreOneJoinRow()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Joined(Member(_dave, _statistik)).Build());

        var row = (await EventsAsync(host)).ShouldHaveSingleItem();
        var daveId = await host.UserIdAsync(_dave);
        row.ShouldSatisfyAllConditions(
            () => row.UserId.ShouldBe(daveId),
            () => row.OccurredAt.ShouldBe(TestTime.Now),
            () => row.Kind.ShouldBe(MembershipEventKind.Join));
    }

    [Fact]
    public async Task Join_WhenTheNewMemberHasNoTrackingRole_ShouldStoreNothing()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Joined(Member(_dave)).Build());

        (await EventsAsync(host)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Join_WhenAnAdministratorJoinsInBootstrapMode_ShouldStoreOneJoinRow()
    {
        await using var host = await HostAsync(trackingRole: false);

        await host.RaiseAsync(Joined(Member(_dave, _admin)).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().Kind.ShouldBe(MembershipEventKind.Join);
    }

    [Fact]
    public async Task Unban_WhenTheUserIsTracked_ShouldStoreOneUnbanRow()
    {
        await using var host = await HostAsync();

        await host.RaiseAsync(Unbanned(_alice).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem().Kind.ShouldBe(MembershipEventKind.Unban);
    }

    [Fact]
    public async Task Unban_WhenTheUserIsNotTracked_ShouldStoreNothing()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Banned(_alice).Build());
        await host.RaiseAsync(Left(_alice).Build());
        host.Time.Advance(TimeSpan.FromDays(1));

        await host.RaiseAsync(Unbanned(_alice).At(host.Time.GetUtcNow()).Build());
        await host.RaiseAsync(Unbanned(_dave).At(host.Time.GetUtcNow()).Build());

        (await KindsAsync(host)).ShouldBe([MembershipEventKind.Ban, MembershipEventKind.Leave]);
    }

    /// <summary>
    /// The payloads carry no timestamp, so a replayed event is stamped anew: the duplicate check cannot rely on
    /// <c>occurred_at</c>.
    /// </summary>
    [Fact]
    public async Task Join_WhenReplayedWithALaterTimestamp_ShouldStoreOneRow()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Joined(Member(_dave, _statistik)).Build());
        host.Time.Advance(TimeSpan.FromSeconds(30));

        await host.RaiseAsync(Joined(Member(_dave, _statistik)).At(host.Time.GetUtcNow()).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Unban_WhenReplayedWithALaterTimestamp_ShouldStoreOneRow()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Unbanned(_alice).Build());
        host.Time.Advance(TimeSpan.FromSeconds(30));

        await host.RaiseAsync(Unbanned(_alice).At(host.Time.GetUtcNow()).Build());

        (await EventsAsync(host)).ShouldHaveSingleItem();
    }

    [Theory]
    [InlineData(HandlerOrder.AfterGuildStateSync)]
    [InlineData(HandlerOrder.BeforeGuildStateSync)]
    public async Task BanAndLeave_WhenReplayedWithLaterTimestamps_ShouldStoreOneRowEach(HandlerOrder order)
    {
        await using var host = await HostAsync(order);
        await host.RaiseAsync(Banned(_alice).Build());
        await host.RaiseAsync(Left(_alice).Build());
        host.Time.Advance(TimeSpan.FromSeconds(1));

        await host.RaiseAsync(Banned(_alice).At(host.Time.GetUtcNow()).Build());
        await host.RaiseAsync(Left(_alice).At(host.Time.GetUtcNow()).Build());

        (await KindsAsync(host)).ShouldBe([MembershipEventKind.Ban, MembershipEventKind.Leave]);
    }

    [Fact]
    public async Task Leave_WhenTheMemberRejoinedTrackedAndLeftAgain_ShouldStoreBothLeaves()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Left(_alice).Build());
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.RaiseAsync(Joined(Member(_alice, _statistik)).At(host.Time.GetUtcNow()).Build());
        host.Time.Advance(TimeSpan.FromSeconds(1));

        await host.RaiseAsync(Left(_alice).At(host.Time.GetUtcNow()).Build());

        (await KindsAsync(host)).ShouldBe([MembershipEventKind.Leave, MembershipEventKind.Join, MembershipEventKind.Leave]);
    }

    [Fact]
    public async Task Join_WhenTheMemberLeftUntrackedAndRejoinedTracked_ShouldStoreBothJoins()
    {
        await using var host = await HostAsync();
        await host.RaiseAsync(Joined(Member(_dave, _statistik)).Build());
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.RaiseAsync(new PlatformMemberUpdatedBuilder().WithMember(Member(_dave)).At(host.Time.GetUtcNow()).Build());
        host.Time.Advance(TimeSpan.FromSeconds(1));
        await host.RaiseAsync(Left(_dave).At(host.Time.GetUtcNow()).Build());
        host.Time.Advance(TimeSpan.FromSeconds(1));

        await host.RaiseAsync(Joined(Member(_dave, _statistik)).At(host.Time.GetUtcNow()).Build());

        (await KindsAsync(host)).ShouldBe([MembershipEventKind.Join, MembershipEventKind.Join]);
    }

    [Fact]
    public async Task Events_WhenStored_ShouldCountThemAsRecordedWithTheirEventType()
    {
        await using var host = await HostAsync();
        using var recorded = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.RecordedCounter);

        await host.RaiseAsync(Joined(Member(_dave, _statistik)).Build());
        await host.RaiseAsync(Unbanned(_dave).Build());
        await host.RaiseAsync(Banned(_alice).Build());
        await host.RaiseAsync(Left(_alice).Build());

        recorded.GetMeasurementSnapshot().Select(m => m.Tags["event_type"]).ShouldBe(
            [nameof(PlatformMemberJoined), nameof(PlatformMemberUnbanned), nameof(PlatformMemberBanned), nameof(PlatformMemberLeft)]);
    }

    [Fact]
    public async Task Events_WhenTheGateDropsThem_ShouldCountThemAsDroppedUntracked()
    {
        await using var host = await HostAsync();
        using var dropped = new MetricCollector<long>(_meters, RecordedEventMetrics.MeterName, RecordedEventMetrics.DroppedCounter);

        await host.RaiseAsync(Joined(Member(_dave)).Build());
        await host.RaiseAsync(Unbanned(_carol).Build());
        await host.RaiseAsync(Banned(_carol).Build());
        await host.RaiseAsync(Left(_carol).Build());

        dropped.GetMeasurementSnapshot().Select(m => m.Tags["reason"]).ShouldBe(["untracked", "untracked", "untracked", "untracked"]);
    }

    public void Dispose() => _meters.Dispose();

    private static PlatformMemberLeftBuilder Left(PlatformRef user) => new PlatformMemberLeftBuilder().ForUser(user);

    private static PlatformMemberBannedBuilder Banned(PlatformRef user) => new PlatformMemberBannedBuilder().ForUser(user);

    private static PlatformMemberUnbannedBuilder Unbanned(PlatformRef user) => new PlatformMemberUnbannedBuilder().ForUser(user);

    private static PlatformMemberJoinedBuilder Joined(PlatformMember member) => new PlatformMemberJoinedBuilder().WithMember(member);

    private static PlatformMember Member(PlatformRef user, params PlatformRef[] roles) =>
        new PlatformMemberBuilder().ForUser(user).WithRoles(roles).Build();

    /// <summary>
    /// Registers the recorder's member-left handler before <c>GuildStateSync</c>'s, so it runs while the leaving user is
    /// still tracked (the dispatcher runs handlers in registration order).
    /// </summary>
    private static void MoveRecorderFirst(IServiceCollection services)
    {
        var recorder = services.Single(d =>
            d.ServiceType == typeof(IPlatformEventHandler<PlatformMemberLeft>) && d.ImplementationType == typeof(MembershipRecorder));
        services.Remove(recorder);
        services.Insert(0, recorder);
    }

    private static Task<List<MembershipEvent>> EventsAsync(TrackingHost host) =>
        host.InScopeAsync((db, _) => db.MembershipEvents.AsNoTracking()
            .OrderBy(e => e.OccurredAt).ThenBy(e => e.Id)
            .ToListAsync(TestContext.Current.CancellationToken));

    private static async Task<List<MembershipEventKind>> KindsAsync(TrackingHost host) =>
        [.. (await EventsAsync(host)).Select(e => e.Kind)];

    private static Task<Guid> GuildIdAsync(TrackingHost host) =>
        host.InScopeAsync(async (_, ids) => (await ids.TryGetGuildAsync(TestPlatform.Guild, TestContext.Current.CancellationToken)).GetValueOrDefault());

    private async Task<TrackingHost> HostAsync(HandlerOrder order = HandlerOrder.AfterGuildStateSync, bool trackingRole = true)
    {
        var host = new TrackingHost(Fixture, services =>
        {
            services.AddSingleton<IMeterFactory>(_meters);
            services.AddMembershipRecording();
            if (order == HandlerOrder.BeforeGuildStateSync)
            {
                MoveRecorderFirst(services);
            }
        });
        if (trackingRole)
        {
            await host.SeedRoleSettingAsync(new RoleSettingBuilder().ForRole(_statistik));
        }

        await host.SyncAsync(new PlatformGuildAvailableBuilder()
            .WithRoles(
                new PlatformRoleBuilder().WithRef(_statistik).Named("Statistik").Build(),
                new PlatformRoleBuilder().WithRef(_admin).Named("Admin").AsAdministrator().Build())
            .WithMembers(Member(_alice, _statistik), Member(_carol))
            .Build());
        return host;
    }
}
