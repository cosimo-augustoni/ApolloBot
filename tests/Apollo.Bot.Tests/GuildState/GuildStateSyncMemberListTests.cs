using Apollo.Bot.GuildState;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;

namespace Apollo.Bot.Tests.GuildState;

/// <summary>
/// The full member list is the only source of departures. When the platform cannot deliver it (empty, failed, timed
/// out), the sync retries a bounded number of times and never marks members as left from a list it does not have.
/// </summary>
public sealed class GuildStateSyncMemberListTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _bob = TestPlatform.User("bob");
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");
    private static readonly PlatformRef _admin = TestPlatform.Role("admin");

    [Fact]
    public async Task GuildAvailable_WhenTheMemberListIsEmpty_ShouldNotMarkAnyMemberLeft()
    {
        await SyncMembersAsync();
        await using var host = new SyncHost(Fixture);

        await host.RaiseAndWaitAsync(Snapshot());

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Members.Values.ShouldAllBe(m => m.LeftAt == null);
        state.MemberRoles.ShouldBe([("alice", "statistik"), ("bob", "admin")]);
        state.Members["bob"].IsAdministrator.ShouldBeTrue();
    }

    [Fact]
    public async Task GuildAvailable_WhenTheMemberDownloadKeepsFailing_ShouldNotMarkAnyMemberLeft()
    {
        await SyncMembersAsync();
        await using var host = new SyncHost(Fixture);
        host.Platform.WithMember(Member(_alice, _statistik)).FailMemberListings(int.MaxValue);

        await host.RaiseAndWaitAsync(Snapshot());

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Members.Values.ShouldAllBe(m => m.LeftAt == null);
        state.RolesOf("bob").ShouldBe(["admin"]);
    }

    [Fact]
    public async Task GuildAvailable_WhenTheMemberDownloadKeepsFailing_ShouldStillSyncRolesAndChannelsButNotReportSynchronized()
    {
        await using var host = new SyncHost(Fixture);
        host.Platform.FailMemberListings(int.MaxValue);

        await host.RaiseAndWaitAsync(Snapshot());

        (await GuildStateView.LoadAsync(Fixture)).Roles.Keys.ShouldBe(["admin", "statistik"], ignoreOrder: true);
        host.Notifications.All.OfType<GuildStateSynchronized>().ShouldBeEmpty();
    }

    [Fact]
    public async Task GuildAvailable_WhenTheMemberDownloadKeepsFailing_ShouldGiveUpAfterTheLastAttempt()
    {
        await using var host = new SyncHost(Fixture);
        host.Platform.FailMemberListings(int.MaxValue);

        await host.RaiseAndWaitAsync(Snapshot());

        host.Platform.MemberListings.ShouldBe(GuildStateSync.MemberListAttempts);
    }

    [Fact]
    public async Task GuildAvailable_WhenTheMemberDownloadFailsOnce_ShouldRetryAfterTheBackoffAndCompleteTheSync()
    {
        await SyncMembersAsync();
        await using var host = new SyncHost(Fixture);
        host.Platform.WithMember(Member(_alice, _statistik)).FailMemberListings(1);

        await host.Time.AdvanceUntilCompletedAsync(host.RaiseAsync(Snapshot()), GuildStateSync.MemberListRetryDelays[0]);

        host.Platform.MemberListings.ShouldBe(2);
        (await GuildStateView.LoadAsync(Fixture)).Members["bob"].LeftAt.ShouldNotBeNull();
        host.Notifications.All[^1].ShouldBeOfType<GuildStateSynchronized>();
    }

    [Fact]
    public async Task GuildAvailable_WhenANewerSnapshotArrivesDuringTheBackoff_ShouldStopTheRetriesOfTheOlderOne()
    {
        await using var host = new SyncHost(Fixture);
        host.Platform.WithMember(Member(_alice, _statistik)).FailMemberListings(1);
        var older = host.RaiseAsync(Snapshot());
        await host.WaitForMemberListingsAsync(1);

        await host.RaiseAsync(Snapshot());
        await host.Time.AdvanceUntilCompletedAsync(older, TimeSpan.FromSeconds(10));

        host.Platform.MemberListings.ShouldBe(2);
        host.Notifications.All.OfType<GuildStateSynchronized>().Count().ShouldBe(1);
    }

    [Fact]
    public async Task GuildAvailable_WhenTheBotStopsDuringTheBackoff_ShouldEndWithoutAnotherAttempt()
    {
        await using var host = new SyncHost(Fixture);
        host.Platform.FailMemberListings(int.MaxValue);
        var sync = host.RaiseAsync(Snapshot());
        await host.WaitForMemberListingsAsync(1);

        host.Lifetime.StopApplication();

        await sync.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        host.Platform.MemberListings.ShouldBe(1);
    }

    private static PlatformGuildAvailable Snapshot() => new PlatformGuildAvailableBuilder()
        .WithRoles(
            new PlatformRoleBuilder().WithRef(_statistik).Named("Statistik").Build(),
            new PlatformRoleBuilder().WithRef(_admin).Named("Admin").AsAdministrator().Build())
        .WithMembers()
        .Build();

    private static Apollo.Platform.Abstractions.Snapshots.PlatformMember Member(PlatformRef user, params PlatformRef[] roles) =>
        new PlatformMemberBuilder().ForUser(user).Named(user.ExternalId).WithRoles(roles).Build();

    /// <summary>
    /// An earlier run of the bot synced alice (Statistik) and bob (Admin).
    /// </summary>
    private async Task SyncMembersAsync()
    {
        await using var host = new SyncHost(Fixture);
        host.Platform.WithMember(Member(_alice, _statistik)).WithMember(Member(_bob, _admin));
        await host.RaiseAndWaitAsync(Snapshot());
    }
}
