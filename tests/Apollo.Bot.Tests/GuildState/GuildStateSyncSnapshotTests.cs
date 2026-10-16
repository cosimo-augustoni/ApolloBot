using Apollo.Bot.GuildState;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;

namespace Apollo.Bot.Tests.GuildState;

public sealed class GuildStateSyncSnapshotTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly Apollo.Platform.Abstractions.PlatformRef _alice = TestPlatform.User("alice");
    private static readonly Apollo.Platform.Abstractions.PlatformRef _bob = TestPlatform.User("bob");
    private static readonly Apollo.Platform.Abstractions.PlatformRef _owner = TestPlatform.User("owner");
    private static readonly Apollo.Platform.Abstractions.PlatformRef _statistik = TestPlatform.Role("statistik");
    private static readonly Apollo.Platform.Abstractions.PlatformRef _admin = TestPlatform.Role("admin");
    private static readonly Apollo.Platform.Abstractions.PlatformRef _category = TestPlatform.Channel("category");
    private static readonly Apollo.Platform.Abstractions.PlatformRef _chat = TestPlatform.Channel("chat");
    private static readonly Apollo.Platform.Abstractions.PlatformRef _lounge = TestPlatform.Channel("lounge");
    private static readonly Apollo.Platform.Abstractions.PlatformRef _thread = TestPlatform.Channel("thread");

    [Fact]
    public async Task GuildAvailable_WhenBotStarts_ShouldStoreGuildRolesChannelsAndMembersWithTheirCurrentState()
    {
        await using var host = new SyncHost(Fixture);

        await host.SyncAsync(Snapshot().Build());

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Guild.ShouldBe(new GuildStateView.GuildRow("Apollo", "owner"));
        state.Roles["statistik"].ShouldBe(new GuildStateView.RoleRow("Statistik", 0x3498DB, 2, IsAdministrator: false, DeletedAt: null));
        state.Roles["admin"].ShouldBe(new GuildStateView.RoleRow("Admin", null, 5, IsAdministrator: true, DeletedAt: null));
        state.Channels["category"].ShouldBe(new GuildStateView.ChannelRow("Allgemein", PlatformChannelType.Category, null, null, null));
        state.Channels["chat"].ShouldBe(new GuildStateView.ChannelRow("chat", PlatformChannelType.Text, "category", null, null));
        state.Channels["lounge"].ShouldBe(new GuildStateView.ChannelRow("Lounge", PlatformChannelType.Voice, "category", null, null));
        state.Users["alice"].ShouldBe(new GuildStateView.UserRow("Alice", "https://cdn.example.test/alice.png"));
        state.Members["alice"].ShouldBe(new GuildStateView.MemberRow(TestTime.Now.AddDays(-30), IsAdministrator: false, IsBot: false, LeftAt: null));
        state.Members["bot"].IsBot.ShouldBeTrue();
        state.MemberRoles.ShouldBe([("alice", "statistik"), ("bob", "admin"), ("bob", "statistik")]);
    }

    [Fact]
    public async Task GuildAvailable_WhenSnapshotListsOnlySomeMembers_ShouldSyncTheFullMemberList()
    {
        await using var host = new SyncHost(Fixture);
        host.Platform.WithMember(new PlatformMemberBuilder().ForUser(_bob).Named("Bob").WithRoles(_statistik).Build());

        await host.SyncAsync(Snapshot().WithMembers(new PlatformMemberBuilder().ForUser(_alice).Build()).Build());

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Members.Keys.ShouldBe(["alice", "bob"], ignoreOrder: true);
        state.RolesOf("bob").ShouldBe(["statistik"]);
    }

    [Fact]
    public async Task GuildAvailable_WhenSnapshotHasAThread_ShouldReferenceTheParentChannel()
    {
        await using var host = new SyncHost(Fixture);

        await host.SyncAsync(Snapshot().Build());

        var thread = (await GuildStateView.LoadAsync(Fixture)).Channels["thread"];
        thread.Type.ShouldBe(PlatformChannelType.Thread);
        thread.ThreadParent.ShouldBe("chat");
        thread.Category.ShouldBeNull();
    }

    [Theory]
    [InlineData("bob", true)]
    [InlineData("owner", true)]
    [InlineData("alice", false)]
    public async Task GuildAvailable_WhenSynced_ShouldFlagAdministratorsByRoleOrOwnership(string user, bool expected)
    {
        await using var host = new SyncHost(Fixture);

        await host.SyncAsync(Snapshot().Build());

        (await GuildStateView.LoadAsync(Fixture)).Members[user].IsAdministrator.ShouldBe(expected);
    }

    [Fact]
    public async Task GuildAvailable_WhenAppliedTwice_ShouldChangeNothing()
    {
        await using var host = new SyncHost(Fixture);
        await host.SyncAsync(Snapshot().Build());
        var first = await GuildStateView.LoadAsync(Fixture);

        await host.SyncAsync(Snapshot().At(TestTime.Now.AddMinutes(5)).Build());

        (await GuildStateView.LoadAsync(Fixture)).Lines().ShouldBe(first.Lines());
    }

    [Fact]
    public async Task GuildAvailable_WhenAppliedTwice_ShouldNotifyMemberChangesOnlyTheFirstTime()
    {
        await using var host = new SyncHost(Fixture);
        await host.SyncAsync(Snapshot().Build());
        host.Notifications.MemberChanges.Count.ShouldBe(4);
        host.Notifications.Clear();

        await host.SyncAsync(Snapshot().Build());

        host.Notifications.MemberChanges.ShouldBeEmpty();
    }

    [Fact]
    public async Task GuildAvailable_WhenSynced_ShouldPublishGuildStateSynchronizedLast()
    {
        await using var host = new SyncHost(Fixture);

        await host.SyncAsync(Snapshot().Build());

        host.Notifications.All[^1].ShouldBe(new GuildStateSynchronized(await host.GuildIdAsync()));
    }

    private static PlatformGuildAvailableBuilder Snapshot() => new PlatformGuildAvailableBuilder()
        .Named("Apollo")
        .OwnedBy(_owner)
        .WithRoles(
            new PlatformRoleBuilder().WithRef(_statistik).Named("Statistik").AtPosition(2).WithColor(0x3498DB).Build(),
            new PlatformRoleBuilder().WithRef(_admin).Named("Admin").AtPosition(5).AsAdministrator().Build())
        .WithChannels(
            new PlatformChannelBuilder().WithRef(_category).Named("Allgemein").OfType(PlatformChannelType.Category).Build(),
            new PlatformChannelBuilder().WithRef(_chat).Named("chat").InCategory(_category).Build(),
            new PlatformChannelBuilder().WithRef(_lounge).Named("Lounge").OfType(PlatformChannelType.Voice).InCategory(_category).Build(),
            new PlatformChannelBuilder().WithRef(_thread).Named("thread").AsThreadOf(_chat).Build())
        .WithMembers(
            new PlatformMemberBuilder().ForUser(_alice).Named("Alice").WithAvatar("https://cdn.example.test/alice.png").WithRoles(_statistik).Build(),
            new PlatformMemberBuilder().ForUser(_bob).Named("Bob").WithRoles(_statistik, _admin).Build(),
            new PlatformMemberBuilder().ForUser(_owner).Named("Owner").Build(),
            new PlatformMemberBuilder().ForUser(TestPlatform.User("bot")).Named("Apollo").AsBot().Build());
}
