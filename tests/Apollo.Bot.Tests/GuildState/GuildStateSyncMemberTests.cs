using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;

namespace Apollo.Bot.Tests.GuildState;

public sealed class GuildStateSyncMemberTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");
    private static readonly PlatformRef _mods = TestPlatform.Role("mods");
    private static readonly PlatformRef _admin = TestPlatform.Role("admin");

    [Fact]
    public async Task MemberUpdated_WhenRolesChange_ShouldReplaceMemberRolesWithTheNewList()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(Updated(_mods, _admin));

        (await GuildStateView.LoadAsync(Fixture)).RolesOf("alice").ShouldBe(["admin", "mods"]);
    }

    [Fact]
    public async Task MemberUpdated_WhenRolesChange_ShouldNotifyOnceForThatMember()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(Updated(_mods));

        host.Notifications.MemberChanges.ShouldBe([await host.UserIdAsync(_alice)]);
    }

    [Fact]
    public async Task MemberUpdated_WhenRolesAreUnchanged_ShouldNotNotify()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(new PlatformMemberUpdatedBuilder()
            .WithMember(new PlatformMemberBuilder().ForUser(_alice).Named("Alice Renamed").WithRoles(_statistik).Build())
            .Build());

        host.Notifications.MemberChanges.ShouldBeEmpty();
        (await GuildStateView.LoadAsync(Fixture)).Users["alice"].DisplayName.ShouldBe("Alice Renamed");
    }

    [Fact]
    public async Task MemberUpdated_WhenMemberGainsAnAdministratorRole_ShouldFlagThemAsAdministrator()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(Updated(_statistik, _admin));

        (await GuildStateView.LoadAsync(Fixture)).Members["alice"].IsAdministrator.ShouldBeTrue();
    }

    [Fact]
    public async Task MemberLeft_WhenMemberLeaves_ShouldSetLeftAtRemoveTheirRolesAndKeepTheUser()
    {
        await using var host = await SyncedHostAsync();
        var leftAt = TestTime.Now.AddHours(1);

        await host.RaiseAsync(new PlatformMemberLeftBuilder().ForUser(_alice).At(leftAt).Build());

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Members["alice"].LeftAt.ShouldBe(leftAt);
        state.RolesOf("alice").ShouldBeEmpty();
        state.Users.ShouldContainKey("alice");
        host.Notifications.MemberChanges.ShouldBe([await host.UserIdAsync(_alice)]);
    }

    [Fact]
    public async Task MemberJoined_WhenMemberComesBackAfterLeaving_ShouldClearLeftAtAndStoreTheirRoles()
    {
        await using var host = await SyncedHostAsync();
        await host.RaiseAsync(new PlatformMemberLeftBuilder().ForUser(_alice).Build());

        await host.RaiseAsync(new PlatformMemberJoinedBuilder()
            .WithMember(new PlatformMemberBuilder().ForUser(_alice).WithRoles(_mods).Build())
            .Build());

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Members["alice"].LeftAt.ShouldBeNull();
        state.RolesOf("alice").ShouldBe(["mods"]);
    }

    [Fact]
    public async Task MemberJoined_WhenUserIsNew_ShouldCreateUserAndMember()
    {
        await using var host = await SyncedHostAsync();
        var carol = TestPlatform.User("carol");

        await host.RaiseAsync(new PlatformMemberJoinedBuilder()
            .WithMember(new PlatformMemberBuilder().ForUser(carol).Named("Carol").Build())
            .Build());

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Users["carol"].DisplayName.ShouldBe("Carol");
        state.Members["carol"].LeftAt.ShouldBeNull();
        host.Notifications.MemberChanges.ShouldBe([await host.UserIdAsync(carol)]);
    }

    private static Apollo.Platform.Abstractions.Events.PlatformMemberUpdated Updated(params PlatformRef[] roles) =>
        new PlatformMemberUpdatedBuilder()
            .WithMember(new PlatformMemberBuilder().ForUser(_alice).WithRoles(roles).Build())
            .Build();

    private async Task<SyncHost> SyncedHostAsync()
    {
        var host = new SyncHost(Fixture);
        await host.SyncAsync(new PlatformGuildAvailableBuilder()
            .WithRoles(
                new PlatformRoleBuilder().WithRef(_statistik).Named("Statistik").Build(),
                new PlatformRoleBuilder().WithRef(_mods).Named("Mods").Build(),
                new PlatformRoleBuilder().WithRef(_admin).Named("Admin").AsAdministrator().Build())
            .WithMembers(new PlatformMemberBuilder().ForUser(_alice).WithRoles(_statistik).Build())
            .Build());
        host.Notifications.Clear();
        return host;
    }
}
