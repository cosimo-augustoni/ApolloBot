using Apollo.Data;
using Apollo.Data.Guilds;
using Apollo.Data.Identity;
using Apollo.Data.Tracking;
using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.IntegrationTests.Data.Tracking;

/// <summary>
/// What the admin settings page reads (#33): the synced roles with member counts, the current settings and mode, the
/// bot's role facts, the preview and the change history.
/// </summary>
public sealed class RoleSettingsQueryTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");
    private static readonly PlatformRef _mitglied = TestPlatform.Role("mitglied");
    private static readonly PlatformRef _apollo = TestPlatform.Role("apollo");
    private static readonly PlatformRef _alice = TestPlatform.User("alice");
    private static readonly PlatformRef _bob = TestPlatform.User("bob");
    private static readonly PlatformRef _carol = TestPlatform.User("carol");
    private static readonly PlatformRef _botUser = TestPlatform.User("apollo-bot");

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task SettingsQuery_ShouldListRolesWithMemberCountsExcludingBots()
    {
        await using var db = Fixture.CreateDbContext();
        var guildId = await SeedGuildAsync(db);
        var statistik = await SeedRoleAsync(db, new RoleBuilder().For(_statistik).Named("Statistik").AtPosition(2).WithColor(0x3498DB));
        var mitglied = await SeedRoleAsync(db, new RoleBuilder().For(_mitglied).Named("Mitglied").AtPosition(1));
        var gone = await SeedRoleAsync(db, new RoleBuilder().For(TestPlatform.Role("gone")).Named("Alt").AtPosition(0).DeletedAt(TestTime.Now));
        await SeedRoleAsync(db, new RoleBuilder().For(TestPlatform.Role("foreign"), new PlatformRef(PlatformKind.Discord, "other-guild")));
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_alice), statistik, mitglied);
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_bob), statistik);
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_botUser).AsBot(), statistik);
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_carol).LeftAt(TestTime.Now), mitglied);

        var settings = await new RoleSettingsQuery(db).GetAsync(guildId, PlatformKind.Discord, Ct);

        settings.Roles.ShouldBe(
        [
            new SettingsRole(statistik, "Statistik", 0x3498DB, 2, IsDeleted: false, MemberCount: 2),
            new SettingsRole(mitglied, "Mitglied", null, 1, IsDeleted: false, MemberCount: 1),
            new SettingsRole(gone, "Alt", null, 0, IsDeleted: true, MemberCount: 0),
        ]);
    }

    [Fact]
    public async Task SettingsQuery_WhenGuildHasRolesOnAnotherPlatform_ShouldListOnlyThePlatformsRoles()
    {
        await using var db = Fixture.CreateDbContext();
        var guildId = await SeedGuildAsync(db);
        var statistik = await SeedRoleAsync(db, new RoleBuilder().For(_statistik).Named("Statistik"));
        var fluxerRole = new Role { Id = Guid.NewGuid(), GuildId = guildId, Name = "Fluxer", Position = 1 };
        db.Roles.Add(fluxerRole);
        db.RolePlatformIds.Add(new RolePlatformId { RoleId = fluxerRole.Id, Platform = PlatformKind.Fluxer, ExternalId = "fluxer-role" });
        await db.SaveChangesAsync(Ct);

        var settings = await new RoleSettingsQuery(db).GetAsync(guildId, PlatformKind.Discord, Ct);

        settings.Roles.Select(r => r.Id).ShouldBe([statistik]);
    }

    [Fact]
    public async Task SettingsQuery_ShouldReturnTheConfiguredRoles()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var statistik = await SeedRoleAsync(db, new RoleBuilder().For(_statistik));
        var mitglied = await SeedRoleAsync(db, new RoleBuilder().For(_mitglied));
        await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);
        await Store(db).SetMemberAccessRolesAsync(guildId, PlatformKind.Discord, [mitglied, statistik], adminId, Ct);

        var settings = await new RoleSettingsQuery(db).GetAsync(guildId, PlatformKind.Discord, Ct);

        settings.TrackingRoleId.ShouldBe(statistik);
        settings.MemberAccessRoleIds.ShouldBe([mitglied, statistik], ignoreOrder: true);
    }

    [Theory]
    [InlineData("never set", TrackingMode.NoTrackingRole)]
    [InlineData("set", TrackingMode.TrackingRole)]
    [InlineData("deleted", TrackingMode.TrackingRoleDeleted)]
    [InlineData("cleared", TrackingMode.NoTrackingRole)]
    public async Task SettingsQuery_ShouldReportTheModeOfTheTrackingRoleSetting(string trackingRole, TrackingMode expected)
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var statistik = await SeedRoleAsync(db, new RoleBuilder().For(_statistik));
        if (trackingRole != "never set")
        {
            await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);
        }

        if (trackingRole == "deleted")
        {
            await db.Roles.Where(r => r.Id == statistik).ExecuteUpdateAsync(s => s.SetProperty(r => r.DeletedAt, TestTime.Now), Ct);
        }
        else if (trackingRole == "cleared")
        {
            await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, null, adminId, Ct);
        }

        var settings = await new RoleSettingsQuery(db).GetAsync(guildId, PlatformKind.Discord, Ct);

        settings.Mode.ShouldBe(expected);
    }

    [Fact]
    public async Task SettingsQuery_WhenTheBotHoldsRoles_ShouldReportItsHighestPositionAndManageRoles()
    {
        await using var db = Fixture.CreateDbContext();
        var guildId = await SeedGuildAsync(db);
        var apollo = await SeedRoleAsync(db, new RoleBuilder().For(_apollo).AtPosition(5).ManagingRoles());
        var mitglied = await SeedRoleAsync(db, new RoleBuilder().For(_mitglied).AtPosition(1));
        var gone = await SeedRoleAsync(db, new RoleBuilder().For(TestPlatform.Role("gone")).AtPosition(9).DeletedAt(TestTime.Now));
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_botUser).AsBot(), apollo, mitglied, gone);
        await SetBotUserAsync(db, _botUser);

        var settings = await new RoleSettingsQuery(db).GetAsync(guildId, PlatformKind.Discord, Ct);

        settings.Bot.ShouldBe(new BotRoleFacts(ManagesRoles: true, HighestRolePosition: 5));
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, true)]
    [InlineData(false, true, true)]
    public async Task SettingsQuery_ShouldTakeManageRolesFromTheBotsRolesOrItsAdministratorFlag(bool roleManagesRoles, bool isAdministrator, bool expected)
    {
        await using var db = Fixture.CreateDbContext();
        var guildId = await SeedGuildAsync(db);
        var builder = new RoleBuilder().For(_apollo).AtPosition(3);
        var apollo = await SeedRoleAsync(db, roleManagesRoles ? builder.ManagingRoles() : builder);
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_botUser).AsBot().AsAdministrator(isAdministrator), apollo);
        await SetBotUserAsync(db, _botUser);

        var settings = await new RoleSettingsQuery(db).GetAsync(guildId, PlatformKind.Discord, Ct);

        settings.Bot.ShouldNotBeNull().ManagesRoles.ShouldBe(expected);
    }

    [Fact]
    public async Task SettingsQuery_WhenTheBotUserIsUnknown_ShouldReportNoBotFacts()
    {
        await using var db = Fixture.CreateDbContext();
        var guildId = await SeedGuildAsync(db);

        var settings = await new RoleSettingsQuery(db).GetAsync(guildId, PlatformKind.Discord, Ct);

        settings.Bot.ShouldBeNull();
    }

    [Fact]
    public async Task Preview_ShouldCountDistinctMembersAcrossRoles()
    {
        await using var db = Fixture.CreateDbContext();
        var guildId = await SeedGuildAsync(db);
        var statistik = await SeedRoleAsync(db, new RoleBuilder().For(_statistik));
        var mitglied = await SeedRoleAsync(db, new RoleBuilder().For(_mitglied));
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_alice), statistik, mitglied);
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_bob), mitglied);
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_carol));
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_botUser).AsBot(), statistik);

        var preview = await new RoleSettingsQuery(db).PreviewAsync(guildId, PlatformKind.Discord, [statistik, mitglied], Ct);

        preview.Members.ShouldBe(2);
    }

    [Fact]
    public async Task Preview_WhenNoTrackingRoleIsSet_ShouldCountEveryHolderAsNewlyTrackedAndNobodyAsStopping()
    {
        await using var db = Fixture.CreateDbContext();
        var guildId = await SeedGuildAsync(db);
        var statistik = await SeedRoleAsync(db, new RoleBuilder().For(_statistik));
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_alice).AsAdministrator(), statistik);
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_bob).AsAdministrator());
        await SeedMemberAsync(db, new GuildMemberBuilder().For(_carol), statistik);

        var preview = await new RoleSettingsQuery(db).PreviewAsync(guildId, PlatformKind.Discord, [statistik], Ct);

        preview.ShouldBe(new RolePreview(Members: 2, NewlyTracked: 2, NoLongerTracked: 0));
    }

    [Fact]
    public async Task History_ShouldReturnNewest20()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var statistik = await SeedRoleAsync(db, new RoleBuilder().For(_statistik).Named("Statistik"));
        var mitglied = await SeedRoleAsync(db, new RoleBuilder().For(_mitglied).Named("Mitglied"));
        for (var i = 0; i < 25; i++)
        {
            _time.Advance(TimeSpan.FromMinutes(1));
            await Store(db).SetMemberAccessRolesAsync(guildId, PlatformKind.Discord, i % 2 == 0 ? [mitglied] : [], adminId, Ct);
        }

        _time.Advance(TimeSpan.FromMinutes(1));
        await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);

        var history = await new RoleSettingsQuery(db).GetHistoryAsync(guildId, PlatformKind.Discord, 20, Ct);

        history.Count.ShouldBe(20);
        history[0].ShouldBe(new RoleSettingHistoryEntry(
            TestTime.Now.AddMinutes(26), "Anna", RoleSettingPurpose.Tracking, RoleSettingAction.Added, statistik, "Statistik"));
        history[1].ShouldBe(new RoleSettingHistoryEntry(
            TestTime.Now.AddMinutes(25), "Anna", RoleSettingPurpose.MemberAccess, RoleSettingAction.Added, mitglied, "Mitglied"));
        history.Select(h => h.ChangedAt).ShouldBeInOrder(SortDirection.Descending);
        history[^1].ChangedAt.ShouldBe(TestTime.Now.AddMinutes(7));
    }

    [Fact]
    public async Task History_ShouldLeaveOutOtherPlatformsAndGuilds()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var otherGuild = new PlatformRef(PlatformKind.Discord, "other-guild");
        var foreign = await SeedRoleAsync(db, new RoleBuilder().For(TestPlatform.Role("foreign"), otherGuild));
        var otherGuildId = await Resolver(db).ResolveGuildAsync(otherGuild, Ct);
        await Store(db).SetTrackingRoleAsync(otherGuildId, PlatformKind.Discord, foreign, adminId, Ct);

        var history = await new RoleSettingsQuery(db).GetHistoryAsync(guildId, PlatformKind.Discord, 20, Ct);

        history.ShouldBeEmpty();
    }

    [Fact]
    public async Task FindSyncedGuild_ShouldReturnTheGuildOnlyOnceTheSyncNamedIt()
    {
        await using var db = Fixture.CreateDbContext();
        var ids = Resolver(db);
        var unsynced = new PlatformRef(PlatformKind.Discord, "unsynced");
        await ids.ResolveGuildAsync(unsynced, Ct);
        var synced = await new GuildBuilder().Named("Apollo").SeedAsync(db, ids, Ct);

        var query = new RoleSettingsQuery(db);

        (await query.FindSyncedGuildAsync(TestPlatform.Guild, Ct)).ShouldBe(synced.Id);
        (await query.FindSyncedGuildAsync(unsynced, Ct)).ShouldBeNull();
        (await query.FindSyncedGuildAsync(new PlatformRef(PlatformKind.Fluxer, "never-seen"), Ct)).ShouldBeNull();
    }

    private RoleSettingsStore Store(ApolloDbContext db) => new(db, _time);

    private static async Task<Guid> SeedGuildAsync(ApolloDbContext db) =>
        (await new GuildBuilder().SeedAsync(db, Resolver(db), Ct)).Id;

    private static async Task<(Guid GuildId, Guid AdminId)> SeedGuildAndAdminAsync(ApolloDbContext db)
    {
        var guildId = await SeedGuildAsync(db);
        var admin = await new UserBuilder().For(TestPlatform.User("anna")).Named("Anna").SeedAsync(db, Resolver(db), Ct);
        return (guildId, admin.Id);
    }

    private static async Task<Guid> SeedRoleAsync(ApolloDbContext db, RoleBuilder role) => (await role.SeedAsync(db, Resolver(db), Ct)).Id;

    private static async Task SeedMemberAsync(ApolloDbContext db, GuildMemberBuilder member, params Guid[] roleIds)
    {
        var row = await member.SeedAsync(db, Resolver(db), Ct);
        db.MemberRoles.AddRange(roleIds.Select(roleId => new MemberRole { GuildId = row.GuildId, UserId = row.UserId, RoleId = roleId }));
        await db.SaveChangesAsync(Ct);
    }

    private static async Task SetBotUserAsync(ApolloDbContext db, PlatformRef bot)
    {
        var botUserId = await Resolver(db).ResolveUserAsync(bot, Ct);
        await db.GuildPlatformIds.Where(g => g.ExternalId == TestPlatform.Guild.ExternalId)
            .ExecuteUpdateAsync(s => s.SetProperty(g => g.BotUserId, botUserId), Ct);
    }

    private static PlatformIdResolver Resolver(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
}
