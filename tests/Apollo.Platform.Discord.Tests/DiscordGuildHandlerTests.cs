using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using NetCord;
using NetCord.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordGuildHandlerTests : IDisposable
{
    private const ulong ForeignGuildId = 222222222222222222;

    private readonly MappingHarness _harness = new();
    private TestGateway? _gateway;

    [Fact]
    public async Task GuildCreate_WhenConfiguredGuildBecomesAvailable_ShouldDispatchTheSnapshot()
    {
        var guild = CachedGuild(new DiscordGuildBuilder()
            .OwnedBy(42)
            .WithRole(7, "Statistik", position: 2, color: 0x3498DB)
            .WithRole(8, "Admin", position: 3, permissions: Permissions.Administrator)
            .WithChannel(10, "Allgemein", ChannelType.CategoryChannel)
            .WithChannel(11, "chat", ChannelType.TextGuildChannel, parentId: 10)
            .WithChannel(16, "thread", ChannelType.PublicGuildThread, parentId: 11)
            .WithMember(42, "alice", roles: [7, 8])
            .WithMember(43, "bob"));

        await Handler().HandleAsync(new GuildCreateEventArgs(DiscordHost.GuildId, guild));

        var snapshot = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformGuildAvailable>();
        snapshot.Guild.ShouldBe(Ref(DiscordHost.GuildId));
        snapshot.OccurredAt.ShouldBe(TestTime.Now);
        snapshot.Name.ShouldBe("Apollo test guild");
        snapshot.Owner.ShouldBe(Ref(42));
        snapshot.Roles.ShouldBe(
        [
            new PlatformRole(Ref(7), "Statistik", 2, IsAdministrator: false, IsManaged: false, Color: 0x3498DB),
            new PlatformRole(Ref(8), "Admin", 3, IsAdministrator: true, IsManaged: false, Color: null),
        ], ignoreOrder: true);
        snapshot.Channels.Select(c => (c.Channel, c.Type, c.Category, c.ThreadParent)).ShouldBe(
        [
            (Ref(10), PlatformChannelType.Category, null, null),
            (Ref(11), PlatformChannelType.Text, Ref(10), null),
            (Ref(16), PlatformChannelType.Thread, null, Ref(11)),
        ], ignoreOrder: true);
        snapshot.Members.Select(m => (m.User, m.Roles.Count)).ShouldBe([(Ref(42), 2), (Ref(43), 0)], ignoreOrder: true);
    }

    [Fact]
    public async Task GuildCreate_WhenGuildIsUnavailable_ShouldNotDispatch()
    {
        await Handler().HandleAsync(new GuildCreateEventArgs(DiscordHost.GuildId, null));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task GuildCreate_WhenGuildIsAnotherGuild_ShouldNotDispatch()
    {
        var guild = CachedGuild(new DiscordGuildBuilder(ForeignGuildId), ForeignGuildId);

        await Handler().HandleAsync(new GuildCreateEventArgs(ForeignGuildId, guild));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    public void Dispose() => _gateway?.Dispose();

    private static PlatformRef Ref(ulong snowflake) => DiscordIds.ToRef(snowflake);

    private Guild CachedGuild(DiscordGuildBuilder builder, ulong guildId = DiscordHost.GuildId)
    {
        _gateway = new TestGateway(builder.Build());
        return _gateway.Client.Cache.Guilds[guildId];
    }

    private DiscordGuildHandler Handler() => new(_harness.Mapper, _harness.Time);
}
