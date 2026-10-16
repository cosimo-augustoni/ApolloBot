using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;

namespace Apollo.Bot.Tests.GuildState;

/// <summary>
/// The gateway delivers events concurrently and the sync lock does not order them, so an update can arrive after the
/// delete or leave it predates. Updates never revive what was removed; only a create, a join or a snapshot does.
/// </summary>
public sealed class GuildStateSyncStaleEventTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly DateTimeOffset _removedAt = TestTime.Now.AddHours(1);
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _mods = TestPlatform.Role("mods");
    private static readonly PlatformRef _chat = TestPlatform.Channel("chat");
    private static readonly PlatformRef _thread = TestPlatform.Channel("thread");

    [Fact]
    public async Task MemberUpdated_WhenMemberHasAlreadyLeft_ShouldKeepThemLeftWithoutRoles()
    {
        await using var host = await SyncedHostAsync();
        await host.RaiseAsync(new PlatformMemberLeftBuilder().ForUser(_alice).At(_removedAt).Build());

        await host.RaiseAsync(new PlatformMemberUpdatedBuilder()
            .WithMember(new PlatformMemberBuilder().ForUser(_alice).WithRoles(_mods).Build())
            .Build());

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Members["alice"].LeftAt.ShouldBe(_removedAt);
        state.RolesOf("alice").ShouldBeEmpty();
    }

    [Fact]
    public async Task RoleUpdated_WhenRoleWasDeleted_ShouldKeepItDeleted()
    {
        await using var host = await SyncedHostAsync();
        await host.RaiseAsync(new PlatformRoleDeletedBuilder().ForRole(_mods).At(_removedAt).Build());

        await host.RaiseAsync(new PlatformRoleUpdatedBuilder()
            .WithRole(new PlatformRoleBuilder().WithRef(_mods).Named("Mods").AsAdministrator().Build())
            .Build());

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Roles["mods"].DeletedAt.ShouldBe(_removedAt);
        state.Members["alice"].IsAdministrator.ShouldBeFalse();
    }

    [Fact]
    public async Task ChannelUpdated_WhenThreadWasDeleted_ShouldKeepItDeleted()
    {
        await using var host = await SyncedHostAsync();
        await host.RaiseAsync(new PlatformChannelDeletedBuilder().ForChannel(_thread).At(_removedAt).Build());

        await host.RaiseAsync(new PlatformChannelUpdatedBuilder().WithChannel(Thread("renamed")).Build());

        (await GuildStateView.LoadAsync(Fixture)).Channels["thread"].DeletedAt.ShouldBe(_removedAt);
    }

    [Fact]
    public async Task ChannelUpdated_WhenChannelWasDeleted_ShouldKeepItDeleted()
    {
        await using var host = await SyncedHostAsync();
        await host.RaiseAsync(new PlatformChannelDeletedBuilder().ForChannel(_chat).At(_removedAt).Build());

        await host.RaiseAsync(new PlatformChannelUpdatedBuilder()
            .WithChannel(new PlatformChannelBuilder().WithRef(_chat).Named("chat 2").Build())
            .Build());

        (await GuildStateView.LoadAsync(Fixture)).Channels["chat"].DeletedAt.ShouldBe(_removedAt);
    }

    [Fact]
    public async Task ChannelDeleted_WhenChannelHasThreads_ShouldMarkTheThreadsDeletedToo()
    {
        await using var host = await SyncedHostAsync();

        await host.RaiseAsync(new PlatformChannelDeletedBuilder().ForChannel(_chat).At(_removedAt).Build());

        (await GuildStateView.LoadAsync(Fixture)).Channels["thread"].DeletedAt.ShouldBe(_removedAt);
    }

    [Fact]
    public async Task GuildAvailable_WhenAChannelWithThreadsWasDeletedWhileOffline_ShouldMarkTheThreadsDeletedToo()
    {
        await using var host = await SyncedHostAsync();

        await host.SyncAsync(new PlatformGuildAvailableBuilder()
            .At(_removedAt)
            .WithRoles(new PlatformRoleBuilder().WithRef(_mods).Named("Mods").Build())
            .WithChannels()
            .WithMembers(new PlatformMemberBuilder().ForUser(_alice).WithRoles(_mods).Build())
            .Build());

        var channels = (await GuildStateView.LoadAsync(Fixture)).Channels;
        channels["chat"].DeletedAt.ShouldBe(_removedAt);
        channels["thread"].DeletedAt.ShouldBe(_removedAt);
    }

    private static PlatformChannel Thread(string name) => new PlatformChannelBuilder().WithRef(_thread).Named(name).AsThreadOf(_chat).Build();

    private async Task<SyncHost> SyncedHostAsync()
    {
        var host = new SyncHost(Fixture);
        await host.SyncAsync(new PlatformGuildAvailableBuilder()
            .WithRoles(new PlatformRoleBuilder().WithRef(_mods).Named("Mods").Build())
            .WithChannels(new PlatformChannelBuilder().WithRef(_chat).Named("chat").Build(), Thread("thread"))
            .WithMembers(new PlatformMemberBuilder().ForUser(_alice).WithRoles(_mods).Build())
            .Build());
        return host;
    }
}
