using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Tracking;
using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.IntegrationTests.Data.Tracking;

/// <summary>
/// The settings write service of the admin page (#33): exactly the rows the bot's 30 s reload reads, and an audit entry
/// with the admin and the time for every addition and removal.
/// </summary>
public sealed class RoleSettingsStoreTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _statistik = TestPlatform.Role("statistik");
    private static readonly PlatformRef _mitglied = TestPlatform.Role("mitglied");
    private static readonly PlatformRef _stammgast = TestPlatform.Role("stammgast");

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RoleSettingsStore_SetTrackingRole_ShouldStoreSingleRowWithAudit()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var statistik = await SeedRoleAsync(db, _statistik);

        var result = await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);

        result.ShouldBe(RoleSettingsWriteResult.Saved);
        var row = (await db.RoleSettings.AsNoTracking().ToListAsync(Ct)).ShouldHaveSingleItem();
        row.ShouldSatisfyAllConditions(
            () => row.GuildId.ShouldBe(guildId),
            () => row.Platform.ShouldBe(PlatformKind.Discord),
            () => row.Purpose.ShouldBe(RoleSettingPurpose.Tracking),
            () => row.RoleId.ShouldBe(statistik),
            () => row.ChangedByUserId.ShouldBe(adminId),
            () => row.ChangedAt.ShouldBe(TestTime.Now));
        var change = (await ChangesAsync(db)).ShouldHaveSingleItem();
        change.ShouldBe(new AuditEntry(RoleSettingPurpose.Tracking, RoleSettingAction.Added, statistik, adminId, TestTime.Now));
    }

    [Fact]
    public async Task RoleSettingsStore_WhenReplacingTrackingRole_ShouldKeepOneRowAndLogRemovalAndAddition()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var statistik = await SeedRoleAsync(db, _statistik);
        var mitglied = await SeedRoleAsync(db, _mitglied);
        await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);
        _time.Advance(TimeSpan.FromHours(1));

        var result = await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, mitglied, adminId, Ct);

        result.ShouldBe(RoleSettingsWriteResult.Saved);
        (await db.RoleSettings.AsNoTracking().ToListAsync(Ct)).ShouldHaveSingleItem().RoleId.ShouldBe(mitglied);
        (await ChangesAsync(db)).Skip(1).ShouldBe(
        [
            new AuditEntry(RoleSettingPurpose.Tracking, RoleSettingAction.Removed, statistik, adminId, TestTime.Now.AddHours(1)),
            new AuditEntry(RoleSettingPurpose.Tracking, RoleSettingAction.Added, mitglied, adminId, TestTime.Now.AddHours(1)),
        ]);
    }

    [Fact]
    public async Task RoleSettingsStore_WhenTheSameTrackingRoleIsSavedAgain_ShouldWriteNoHistory()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var statistik = await SeedRoleAsync(db, _statistik);
        await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);
        _time.Advance(TimeSpan.FromHours(1));

        var result = await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);

        result.ShouldBe(RoleSettingsWriteResult.Saved);
        (await db.RoleSettings.AsNoTracking().SingleAsync(Ct)).ChangedAt.ShouldBe(TestTime.Now);
        (await ChangesAsync(db)).Count.ShouldBe(1);
    }

    [Theory]
    [InlineData(InvalidRole.Deleted)]
    [InlineData(InvalidRole.OtherGuild)]
    [InlineData(InvalidRole.OtherPlatform)]
    [InlineData(InvalidRole.Unknown)]
    public async Task RoleSettingsStore_WhenRoleDeletedOrForeign_ShouldReject(InvalidRole kind)
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var statistik = await SeedRoleAsync(db, _statistik);
        await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);
        var invalid = await SeedInvalidRoleAsync(db, guildId, kind);

        var tracking = await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, invalid, adminId, Ct);
        var access = await Store(db).SetMemberAccessRolesAsync(guildId, PlatformKind.Discord, [statistik, invalid], adminId, Ct);

        tracking.ShouldBe(RoleSettingsWriteResult.InvalidRole);
        access.ShouldBe(RoleSettingsWriteResult.InvalidRole);
        (await db.RoleSettings.AsNoTracking().ToListAsync(Ct)).ShouldHaveSingleItem().RoleId.ShouldBe(statistik);
        (await ChangesAsync(db)).Count.ShouldBe(1);
    }

    [Fact]
    public async Task RoleSettingsStore_SetMemberAccessRoles_ShouldStoreExactSetAndLogDiff()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var statistik = await SeedRoleAsync(db, _statistik);
        var mitglied = await SeedRoleAsync(db, _mitglied);
        var stammgast = await SeedRoleAsync(db, _stammgast);
        await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);
        await Store(db).SetMemberAccessRolesAsync(guildId, PlatformKind.Discord, [mitglied, statistik], adminId, Ct);
        _time.Advance(TimeSpan.FromHours(1));

        var result = await Store(db).SetMemberAccessRolesAsync(guildId, PlatformKind.Discord, [mitglied, stammgast, stammgast], adminId, Ct);

        result.ShouldBe(RoleSettingsWriteResult.Saved);
        var access = await db.RoleSettings.AsNoTracking().Where(s => s.Purpose == RoleSettingPurpose.MemberAccess).ToListAsync(Ct);
        access.Select(s => s.RoleId).ShouldBe([mitglied, stammgast], ignoreOrder: true);
        (await db.RoleSettings.AsNoTracking().SingleAsync(s => s.Purpose == RoleSettingPurpose.Tracking, Ct)).RoleId.ShouldBe(statistik);
        (await ChangesAsync(db)).Skip(3).ShouldBe(
        [
            new AuditEntry(RoleSettingPurpose.MemberAccess, RoleSettingAction.Removed, statistik, adminId, TestTime.Now.AddHours(1)),
            new AuditEntry(RoleSettingPurpose.MemberAccess, RoleSettingAction.Added, stammgast, adminId, TestTime.Now.AddHours(1)),
        ]);
    }

    [Fact]
    public async Task RoleSettingsStore_WhenTrackingRoleIsCleared_ShouldLogTheRemovalWithTheAdminAndTime()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var statistik = await SeedRoleAsync(db, _statistik);
        await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);
        _time.Advance(TimeSpan.FromHours(1));

        var result = await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, null, adminId, Ct);

        result.ShouldBe(RoleSettingsWriteResult.Saved);
        (await db.RoleSettings.AsNoTracking().AnyAsync(Ct)).ShouldBeFalse();
        (await ChangesAsync(db)).Last().ShouldBe(
            new AuditEntry(RoleSettingPurpose.Tracking, RoleSettingAction.Removed, statistik, adminId, TestTime.Now.AddHours(1)));
    }

    [Fact]
    public async Task RoleSettingsStore_WhenMemberAccessRoleIsRemoved_ShouldLogTheRemovalWithTheAdminAndTime()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var mitglied = await SeedRoleAsync(db, _mitglied);
        await Store(db).SetMemberAccessRolesAsync(guildId, PlatformKind.Discord, [mitglied], adminId, Ct);
        _time.Advance(TimeSpan.FromHours(1));

        await Store(db).SetMemberAccessRolesAsync(guildId, PlatformKind.Discord, [], adminId, Ct);

        (await db.RoleSettings.AsNoTracking().AnyAsync(Ct)).ShouldBeFalse();
        (await ChangesAsync(db)).Last().ShouldBe(
            new AuditEntry(RoleSettingPurpose.MemberAccess, RoleSettingAction.Removed, mitglied, adminId, TestTime.Now.AddHours(1)));
    }

    [Fact]
    public async Task RoleSettings_WhenARowIsDeletedOutsideTheStoreAfterAStoreWrite_ShouldRecordNoAuthor()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, adminId) = await SeedGuildAndAdminAsync(db);
        var statistik = await SeedRoleAsync(db, _statistik);
        await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);

        await db.RoleSettings.ExecuteDeleteAsync(Ct);

        (await ChangesAsync(db)).Last().ChangedBy.ShouldBeNull();
    }

    [Fact]
    public async Task RoleSettingsStore_WhenAnotherAdminSetsATrackingRoleAtTheSameTime_ShouldReturnConflictAndKeepTheirs()
    {
        Guid guildId, adminId, statistik, mitglied;
        await using (var setup = Fixture.CreateDbContext())
        {
            (guildId, adminId) = await SeedGuildAndAdminAsync(setup);
            statistik = await SeedRoleAsync(setup, _statistik);
            mitglied = await SeedRoleAsync(setup, _mitglied);
        }

        var interceptor = new CompetingWriteInterceptor(EntityState.Added, async () =>
        {
            await using var other = Fixture.CreateDbContext();
            (await Store(other).SetTrackingRoleAsync(guildId, PlatformKind.Discord, mitglied, adminId, Ct)).ShouldBe(RoleSettingsWriteResult.Saved);
        });
        await using var db = CreateDbContext(interceptor);

        var result = await Store(db).SetTrackingRoleAsync(guildId, PlatformKind.Discord, statistik, adminId, Ct);

        result.ShouldBe(RoleSettingsWriteResult.Conflict);
        await using var check = Fixture.CreateDbContext();
        (await check.RoleSettings.AsNoTracking().ToListAsync(Ct)).ShouldHaveSingleItem().RoleId.ShouldBe(mitglied);
        (await ChangesAsync(check)).ShouldHaveSingleItem().RoleId.ShouldBe(mitglied);
    }

    [Fact]
    public async Task RoleSettingsStore_WhenAnotherAdminRemovesTheSameRoleAtTheSameTime_ShouldReturnConflict()
    {
        Guid guildId, adminId;
        await using (var setup = Fixture.CreateDbContext())
        {
            (guildId, adminId) = await SeedGuildAndAdminAsync(setup);
            var mitglied = await SeedRoleAsync(setup, _mitglied);
            await Store(setup).SetMemberAccessRolesAsync(guildId, PlatformKind.Discord, [mitglied], adminId, Ct);
        }

        var interceptor = new CompetingWriteInterceptor(EntityState.Deleted, async () =>
        {
            await using var other = Fixture.CreateDbContext();
            (await Store(other).SetMemberAccessRolesAsync(guildId, PlatformKind.Discord, [], adminId, Ct)).ShouldBe(RoleSettingsWriteResult.Saved);
        });
        await using var db = CreateDbContext(interceptor);

        var result = await Store(db).SetMemberAccessRolesAsync(guildId, PlatformKind.Discord, [], adminId, Ct);

        result.ShouldBe(RoleSettingsWriteResult.Conflict);
        await using var check = Fixture.CreateDbContext();
        (await check.RoleSettings.AnyAsync(Ct)).ShouldBeFalse();
        (await ChangesAsync(check)).Select(c => c.Action).ShouldBe([RoleSettingAction.Added, RoleSettingAction.Removed]);
    }

    public enum InvalidRole
    {
        Deleted,
        OtherGuild,
        OtherPlatform,
        Unknown,
    }

    private RoleSettingsStore Store(ApolloDbContext db) => new(db, _time);

    private ApolloDbContext CreateDbContext(IInterceptor interceptor) =>
        new(new DbContextOptionsBuilder<ApolloDbContext>(ApolloDbContext.CreateOptions(Fixture.ConnectionString)).AddInterceptors(interceptor).Options);

    private static async Task<(Guid GuildId, Guid AdminId)> SeedGuildAndAdminAsync(ApolloDbContext db)
    {
        var ids = Resolver(db);
        var guildId = await ids.ResolveGuildAsync(TestPlatform.Guild, Ct);
        var admin = await new UserBuilder().For(TestPlatform.User("anna")).Named("Anna").SeedAsync(db, ids, Ct);
        return (guildId, admin.Id);
    }

    private static async Task<Guid> SeedRoleAsync(ApolloDbContext db, PlatformRef role) =>
        (await new RoleBuilder().For(role).SeedAsync(db, Resolver(db), Ct)).Id;

    private static async Task<Guid> SeedInvalidRoleAsync(ApolloDbContext db, Guid guildId, InvalidRole kind)
    {
        switch (kind)
        {
            case InvalidRole.Deleted:
                return (await new RoleBuilder().For(TestPlatform.Role("gone")).DeletedAt(TestTime.Now.AddDays(-1)).SeedAsync(db, Resolver(db), Ct)).Id;
            case InvalidRole.OtherGuild:
                return (await new RoleBuilder().For(TestPlatform.Role("foreign"), new PlatformRef(PlatformKind.Discord, "other-guild"))
                    .SeedAsync(db, Resolver(db), Ct)).Id;
            case InvalidRole.OtherPlatform:
                // A Fluxer role of the same internal guild: the settings are per (guild, platform).
                var role = new Role { Id = Guid.NewGuid(), GuildId = guildId, Name = "Fluxer", Position = 1 };
                db.Roles.Add(role);
                db.RolePlatformIds.Add(new RolePlatformId { RoleId = role.Id, Platform = PlatformKind.Fluxer, ExternalId = "fluxer-role" });
                await db.SaveChangesAsync(Ct);
                return role.Id;
            default:
                return Guid.NewGuid();
        }
    }

    private static async Task<List<AuditEntry>> ChangesAsync(ApolloDbContext db) =>
        await db.RoleSettingChanges.AsNoTracking()
            .OrderBy(c => c.Id)
            .Select(c => new AuditEntry(c.Purpose, c.Action, c.RoleId, c.ChangedByUserId, c.ChangedAt))
            .ToListAsync(Ct);

    private static PlatformIdResolver Resolver(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);

    private sealed record AuditEntry(RoleSettingPurpose Purpose, RoleSettingAction Action, Guid RoleId, Guid? ChangedBy, DateTimeOffset ChangedAt);
}
