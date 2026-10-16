using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Abstractions.Snapshots;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using NetCord;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordRoleHandlerTests
{
    private const ulong ForeignGuildId = 222222222222222222;

    private readonly MappingHarness _harness = new();

    [Fact]
    public async Task RoleCreate_WhenRoleIsCreated_ShouldDispatchRoleCreatedWithItsSnapshot()
    {
        var role = NetCordPayloads.Role(DiscordHost.GuildId, 7, "Statistik", position: 2, color: 0x3498DB);

        await ((IRoleCreateGatewayHandler)Handler()).HandleAsync(role);

        var created = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformRoleCreated>();
        created.OccurredAt.ShouldBe(TestTime.Now);
        created.Role.ShouldBe(new PlatformRole(Ref(7), "Statistik", 2, IsAdministrator: false, IsManaged: false, Color: 0x3498DB));
    }

    [Fact]
    public async Task RoleUpdate_WhenRoleGainsAdministrator_ShouldDispatchRoleUpdatedWithTheFlag()
    {
        var role = NetCordPayloads.Role(DiscordHost.GuildId, 7, "Statistik", permissions: Permissions.Administrator);

        await ((IRoleUpdateGatewayHandler)Handler()).HandleAsync(role);

        var updated = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformRoleUpdated>();
        updated.Role.Role.ShouldBe(Ref(7));
        updated.Role.IsAdministrator.ShouldBeTrue();
    }

    [Fact]
    public async Task RoleUpdate_WhenRoleIsEveryone_ShouldNotDispatch()
    {
        var everyone = NetCordPayloads.Role(DiscordHost.GuildId, DiscordHost.GuildId, "@everyone");

        await ((IRoleUpdateGatewayHandler)Handler()).HandleAsync(everyone);

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    [Fact]
    public async Task RoleDelete_WhenRoleIsDeleted_ShouldDispatchRoleDeleted()
    {
        await Handler().HandleAsync(NetCordPayloads.RoleDelete(DiscordHost.GuildId, 7));

        var deleted = _harness.Dispatcher.Events.ShouldHaveSingleItem().ShouldBeOfType<PlatformRoleDeleted>();
        deleted.Role.ShouldBe(Ref(7));
        deleted.OccurredAt.ShouldBe(TestTime.Now);
    }

    [Fact]
    public async Task RoleDelete_WhenRoleIsFromAnotherGuild_ShouldNotDispatch()
    {
        await Handler().HandleAsync(NetCordPayloads.RoleDelete(ForeignGuildId, 7));

        _harness.Dispatcher.Events.ShouldBeEmpty();
    }

    private static PlatformRef Ref(ulong snowflake) => DiscordIds.ToRef(snowflake);

    private DiscordRoleHandler Handler() => new(_harness.Mapper, _harness.Time);
}
