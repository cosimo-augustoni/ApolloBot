using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;

namespace Apollo.Bot.Tests.GuildState;

public sealed class GuildStateSyncRoleTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _bob = TestPlatform.User("bob");
    private static readonly PlatformRef _carol = TestPlatform.User("carol");
    private static readonly PlatformRef _dave = TestPlatform.User("dave");
    private static readonly PlatformRef _owner = TestPlatform.User("owner");
    private static readonly PlatformRef _mods = TestPlatform.Role("mods");
    private static readonly PlatformRef _admin = TestPlatform.Role("admin");

    [Fact]
    public async Task RoleUpdated_WhenRoleGainsAdministrator_ShouldFlagEveryMemberHoldingIt()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(new PlatformRoleUpdatedBuilder()
            .WithRole(new PlatformRoleBuilder().WithRef(_mods).Named("Mods").AsAdministrator().Build())
            .Build());

        var members = (await GuildStateView.LoadAsync(Fixture)).Members;
        members["alice"].IsAdministrator.ShouldBeTrue();
        members["bob"].IsAdministrator.ShouldBeTrue();
        members["dave"].IsAdministrator.ShouldBeFalse();
    }

    [Fact]
    public async Task RoleUpdated_WhenRoleDropsAdministrator_ShouldClearTheFlagUnlessAnotherReasonRemains()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(new PlatformRoleUpdatedBuilder()
            .WithRole(new PlatformRoleBuilder().WithRef(_admin).Named("Admin").AsAdministrator(false).Build())
            .Build());

        var members = (await GuildStateView.LoadAsync(Fixture)).Members;
        members["carol"].IsAdministrator.ShouldBeFalse();
        members["owner"].IsAdministrator.ShouldBeTrue();
    }

    [Fact]
    public async Task RoleUpdated_WhenAdministratorChanges_ShouldNotifyEveryMemberWhoseFlagChanged()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(new PlatformRoleUpdatedBuilder()
            .WithRole(new PlatformRoleBuilder().WithRef(_mods).Named("Mods").AsAdministrator().Build())
            .Build());

        host.Notifications.MemberChanges.ShouldBe([await host.UserIdAsync(_alice), await host.UserIdAsync(_bob)], ignoreOrder: true);
    }

    [Fact]
    public async Task RoleUpdated_WhenOnlyTheNameChanges_ShouldUpdateTheRowWithoutNotifying()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(new PlatformRoleUpdatedBuilder()
            .WithRole(new PlatformRoleBuilder().WithRef(_mods).Named("Moderation").AtPosition(4).WithColor(0xFF0000).Build())
            .Build());

        (await GuildStateView.LoadAsync(Fixture)).Roles["mods"]
            .ShouldBe(new GuildStateView.RoleRow("Moderation", 0xFF0000, 4, IsAdministrator: false, DeletedAt: null));
        host.Notifications.MemberChanges.ShouldBeEmpty();
    }

    [Fact]
    public async Task RoleCreated_WhenRoleIsNew_ShouldStoreIt()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(new PlatformRoleCreatedBuilder()
            .WithRole(new PlatformRoleBuilder().WithRef(TestPlatform.Role("vip")).Named("VIP").AtPosition(3).Build())
            .Build());

        (await GuildStateView.LoadAsync(Fixture)).Roles["vip"].Name.ShouldBe("VIP");
    }

    [Fact]
    public async Task RoleDeleted_WhenProcessed_ShouldSetDeletedAtKeepTheRowAndRemoveItsMemberRoles()
    {
        await using var host = await SyncedHostAsync();
        var deletedAt = TestTime.Now.AddHours(1);

        await host.RaiseAsync(new PlatformRoleDeletedBuilder().ForRole(_mods).At(deletedAt).Build());

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Roles["mods"].DeletedAt.ShouldBe(deletedAt);
        state.MemberRoles.ShouldNotContain(r => r.Role == "mods");
        host.Notifications.MemberChanges.ShouldBe([await host.UserIdAsync(_alice), await host.UserIdAsync(_bob)], ignoreOrder: true);
    }

    [Fact]
    public async Task RoleDeleted_WhenRoleGrantedAdministrator_ShouldClearTheFlagOfItsHolders()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(new PlatformRoleDeletedBuilder().ForRole(_admin).Build());

        (await GuildStateView.LoadAsync(Fixture)).Members["carol"].IsAdministrator.ShouldBeFalse();
    }

    private async Task<SyncHost> SyncedHostAsync()
    {
        var host = new SyncHost(Fixture);
        await host.SyncAsync(new PlatformGuildAvailableBuilder()
            .OwnedBy(_owner)
            .WithRoles(
                new PlatformRoleBuilder().WithRef(_mods).Named("Mods").Build(),
                new PlatformRoleBuilder().WithRef(_admin).Named("Admin").AsAdministrator().Build())
            .WithMembers(
                new PlatformMemberBuilder().ForUser(_alice).WithRoles(_mods).Build(),
                new PlatformMemberBuilder().ForUser(_bob).WithRoles(_mods).Build(),
                new PlatformMemberBuilder().ForUser(_carol).WithRoles(_admin).Build(),
                new PlatformMemberBuilder().ForUser(_dave).Build(),
                new PlatformMemberBuilder().ForUser(_owner).WithRoles(_admin).Build())
            .Build());
        host.Notifications.Clear();
        return host;
    }
}
