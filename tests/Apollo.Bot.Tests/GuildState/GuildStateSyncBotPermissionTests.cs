using Apollo.Platform.Abstractions;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Bot.Tests.GuildState;

/// <summary>
/// The guild state the admin settings page checks the bot's permissions from (#33): which roles grant Manage Roles and
/// which user is the bot itself.
/// </summary>
public sealed class GuildStateSyncBotPermissionTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _bot = TestPlatform.User("apollo-bot");
    private static readonly PlatformRef _apollo = TestPlatform.Role("apollo");
    private static readonly PlatformRef _mods = TestPlatform.Role("mods");

    [Fact]
    public async Task GuildAvailable_ShouldStoreTheBotUserOfTheGuildOnThatPlatform()
    {
        await using var host = new SyncHost(Fixture);

        await host.SyncAsync(Snapshot().Build());

        var botUserId = await host.InScopeAsync((db, _) => db.GuildPlatformIds
            .Where(g => g.Platform == TestPlatform.Guild.Platform && g.ExternalId == TestPlatform.Guild.ExternalId)
            .Select(g => g.BotUserId)
            .SingleAsync(TestContext.Current.CancellationToken));
        botUserId.ShouldBe(await host.UserIdAsync(_bot));
    }

    [Fact]
    public async Task GuildAvailable_ShouldStoreWhichRolesGrantManageRoles()
    {
        await using var host = new SyncHost(Fixture);

        await host.SyncAsync(Snapshot().Build());

        (await ManagesRolesAsync(host)).ShouldBe(new Dictionary<string, bool> { ["apollo"] = true, ["mods"] = false });
    }

    [Fact]
    public async Task RoleUpdated_WhenRoleGainsManageRoles_ShouldStoreIt()
    {
        await using var host = new SyncHost(Fixture);
        await host.SyncAsync(Snapshot().Build());

        await host.RaiseAsync(new PlatformRoleUpdatedBuilder()
            .WithRole(new PlatformRoleBuilder().WithRef(_mods).Named("Mods").ManagingRoles().Build())
            .Build());

        (await ManagesRolesAsync(host))["mods"].ShouldBeTrue();
    }

    private static PlatformGuildAvailableBuilder Snapshot() => new PlatformGuildAvailableBuilder()
        .WithBot(_bot)
        .WithRoles(
            new PlatformRoleBuilder().WithRef(_apollo).Named("Apollo").AtPosition(5).ManagingRoles().Build(),
            new PlatformRoleBuilder().WithRef(_mods).Named("Mods").AtPosition(2).Build())
        .WithMembers(new PlatformMemberBuilder().ForUser(_bot).AsBot().WithRoles(_apollo).Build());

    private static Task<Dictionary<string, bool>> ManagesRolesAsync(SyncHost host) =>
        host.InScopeAsync((db, _) => db.Roles
            .Join(db.RolePlatformIds, r => r.Id, m => m.RoleId, (r, m) => new { m.ExternalId, r.ManagesRoles })
            .ToDictionaryAsync(r => r.ExternalId, r => r.ManagesRoles, TestContext.Current.CancellationToken));
}
