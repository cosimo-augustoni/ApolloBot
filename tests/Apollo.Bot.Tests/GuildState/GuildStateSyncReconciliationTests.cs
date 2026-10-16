using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;

namespace Apollo.Bot.Tests.GuildState;

/// <summary>
/// The bot was offline while things changed; the snapshot after the reconnect brings the database up to date.
/// </summary>
public sealed class GuildStateSyncReconciliationTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly DateTimeOffset _reconnectedAt = TestTime.Now.AddDays(1);
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _bob = TestPlatform.User("bob");
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");
    private static readonly PlatformRef _mods = TestPlatform.Role("mods");
    private static readonly PlatformRef _chat = TestPlatform.Channel("chat");
    private static readonly PlatformRef _memes = TestPlatform.Channel("memes");
    private static readonly PlatformRef _thread = TestPlatform.Channel("thread");

    [Fact]
    public async Task GuildAvailable_WhenARoleWasDeletedWhileOffline_ShouldMarkItDeletedAndRemoveItsMemberRoles()
    {
        await SyncBeforeAsync();

        await using var host = await ReconnectAsync(Before().WithRoles(Role(_statistik)).WithMembers(Member(_alice, _statistik), Member(_bob)));

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Roles["mods"].DeletedAt.ShouldBe(_reconnectedAt);
        state.Roles["statistik"].DeletedAt.ShouldBeNull();
        state.MemberRoles.ShouldBe([("alice", "statistik")]);
    }

    [Fact]
    public async Task GuildAvailable_WhenAChannelWasDeletedWhileOffline_ShouldMarkItDeleted()
    {
        await SyncBeforeAsync();

        await using var host = await ReconnectAsync(Before().WithChannels(Channel(_chat), Thread()));

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Channels["memes"].DeletedAt.ShouldBe(_reconnectedAt);
        state.Channels["chat"].DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task GuildAvailable_WhenAThreadIsMissingFromTheSnapshot_ShouldKeepItBecauseItMayOnlyBeArchived()
    {
        await SyncBeforeAsync();

        await using var host = await ReconnectAsync(Before().WithChannels(Channel(_chat), Channel(_memes)));

        (await GuildStateView.LoadAsync(Fixture)).Channels["thread"].DeletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task GuildAvailable_WhenAMemberLeftWhileOffline_ShouldSetLeftAtAndRemoveTheirMemberRoles()
    {
        await SyncBeforeAsync();

        await using var host = await ReconnectAsync(Before().WithMembers(Member(_alice, _statistik, _mods)));

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Members["bob"].LeftAt.ShouldBe(_reconnectedAt);
        state.RolesOf("bob").ShouldBeEmpty();
        state.Users.ShouldContainKey("bob");
        host.Notifications.MemberChanges.ShouldBe([await host.UserIdAsync(_bob)]);
    }

    [Fact]
    public async Task GuildAvailable_WhenAMemberLostARoleWhileOffline_ShouldMatchTheSnapshotExactly()
    {
        await SyncBeforeAsync();

        await using var host = await ReconnectAsync(Before().WithMembers(Member(_alice, _mods), Member(_bob)));

        (await GuildStateView.LoadAsync(Fixture)).MemberRoles.ShouldBe([("alice", "mods")]);
    }

    [Fact]
    public async Task GuildAvailable_WhenTheDatabaseHasRowsTheGuildNoLongerHas_ShouldMarkThemDeleted()
    {
        await using var host = new SyncHost(Fixture);
        var gone = TestPlatform.Role("gone");
        var oldChannel = TestPlatform.Channel("old");
        await host.InScopeAsync((db, ids) => new RoleBuilder().For(gone).Named("Gone").SeedAsync(db, ids, TestContext.Current.CancellationToken));
        await host.InScopeAsync((db, ids) => new ChannelBuilder().For(oldChannel).Named("old").SeedAsync(db, ids, TestContext.Current.CancellationToken));

        await host.SyncAsync(Before().At(_reconnectedAt).Build());

        var state = await GuildStateView.LoadAsync(Fixture);
        state.Roles["gone"].DeletedAt.ShouldBe(_reconnectedAt);
        state.Channels["old"].DeletedAt.ShouldBe(_reconnectedAt);
    }

    private static PlatformRole Role(PlatformRef role) => new PlatformRoleBuilder().WithRef(role).Named(role.ExternalId).Build();

    private static PlatformChannel Channel(PlatformRef channel) => new PlatformChannelBuilder().WithRef(channel).Named(channel.ExternalId).Build();

    private static PlatformChannel Thread() => new PlatformChannelBuilder().WithRef(_thread).Named("thread").AsThreadOf(_chat).Build();

    private static PlatformMember Member(PlatformRef user, params PlatformRef[] roles) =>
        new PlatformMemberBuilder().ForUser(user).Named(user.ExternalId).WithRoles(roles).Build();

    private static PlatformGuildAvailableBuilder Before() => new PlatformGuildAvailableBuilder()
        .WithRoles(Role(_statistik), Role(_mods))
        .WithChannels(Channel(_chat), Channel(_memes), Thread())
        .WithMembers(Member(_alice, _statistik, _mods), Member(_bob, _mods));

    /// <summary>
    /// The bot starts again (a new host, so a new platform with exactly the snapshot's members) and syncs.
    /// </summary>
    private async Task<SyncHost> ReconnectAsync(PlatformGuildAvailableBuilder snapshot)
    {
        var host = new SyncHost(Fixture);
        await host.SyncAsync(snapshot.At(_reconnectedAt).Build());
        return host;
    }

    private async Task SyncBeforeAsync()
    {
        await using var host = new SyncHost(Fixture);
        await host.SyncAsync(Before().Build());
    }
}
