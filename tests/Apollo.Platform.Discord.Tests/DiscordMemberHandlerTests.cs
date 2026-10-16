using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using NetCord;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordMemberHandlerTests : IDisposable
{
    private const ulong ForeignGuildId = 222222222222222222;

    private readonly MappingHarness _harness = new();
    private readonly TestGateway _gateway = new(new DiscordGuildBuilder()
        .WithRole(7, "Statistik")
        .WithRole(8, "Admin", permissions: Permissions.Administrator)
        .Build());

    [Fact]
    public async Task GuildUserAdd_WhenMemberJoins_ShouldDispatchMemberJoinedWithTheirRoles()
    {
        var member = NetCordPayloads.Member(DiscordHost.GuildId, 42, "alice", roles: [7]);

        await ((IGuildUserAddGatewayHandler)Handler()).HandleAsync(member);

        var joined = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMemberJoined>();
        joined.Guild.ShouldBe(Ref(DiscordHost.GuildId));
        joined.OccurredAt.ShouldBe(TestTime.Now);
        joined.Member.User.ShouldBe(Ref(42));
        joined.Member.DisplayName.ShouldBe("alice");
        joined.Member.Roles.ShouldBe([Ref(7)]);
    }

    [Fact]
    public async Task GuildUserUpdate_WhenRolesChange_ShouldDispatchMemberUpdatedWithTheNewRoles()
    {
        var member = NetCordPayloads.Member(DiscordHost.GuildId, 42, roles: [7, 8]);

        await ((IGuildUserUpdateGatewayHandler)Handler()).HandleAsync(member);

        var updated = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMemberUpdated>();
        updated.Member.User.ShouldBe(Ref(42));
        updated.Member.Roles.ShouldBe([Ref(7), Ref(8)], ignoreOrder: true);
        updated.Member.IsAdministrator.ShouldBeTrue();
    }

    [Fact]
    public async Task GuildUserRemove_WhenMemberLeaves_ShouldDispatchMemberLeft()
    {
        await Handler().HandleAsync(NetCordPayloads.GuildUserRemove(DiscordHost.GuildId, userId: 42));

        var left = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformMemberLeft>();
        left.User.ShouldBe(Ref(42));
        left.OccurredAt.ShouldBe(TestTime.Now);
    }

    [Fact]
    public async Task GuildUserUpdate_WhenMemberIsFromAnotherGuild_ShouldNotDispatch()
    {
        var member = NetCordPayloads.Member(ForeignGuildId, 42, roles: [7]);

        await ((IGuildUserUpdateGatewayHandler)Handler()).HandleAsync(member);

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    public void Dispose() => _gateway.Dispose();

    private static PlatformRef Ref(ulong snowflake) => DiscordIds.ToRef(snowflake);

    private DiscordMemberHandler Handler() => new(_gateway.Client, _harness.Mapper, _harness.Time);
}
