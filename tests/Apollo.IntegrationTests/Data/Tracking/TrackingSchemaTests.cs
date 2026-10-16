using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Tracking;
using Apollo.Domain.Tracking;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests.Data.Tracking;

public sealed class TrackingSchemaTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RoleSettings_WhenASecondTrackingRowIsInsertedForTheSameGuildAndPlatform_ShouldBeRejected()
    {
        await using var db = Fixture.CreateDbContext();
        var ids = Resolver(db);
        await new RoleSettingBuilder().ForRole(TestPlatform.Role("statistik")).SeedAsync(db, ids, Ct);

        await using var second = Fixture.CreateDbContext();
        var insert = () => new RoleSettingBuilder().ForRole(TestPlatform.Role("other")).SeedAsync(second, Resolver(second), Ct);

        (await insert.ShouldThrowAsync<DbUpdateException>()).InnerException!.Message.ShouldContain("duplicate key");
    }

    [Fact]
    public async Task RoleSettings_WhenSeveralMemberAccessRowsAreInserted_ShouldAcceptThem()
    {
        await using var db = Fixture.CreateDbContext();
        var ids = Resolver(db);

        await new RoleSettingBuilder().ForRole(TestPlatform.Role("members")).WithPurpose(RoleSettingPurpose.MemberAccess).SeedAsync(db, ids, Ct);
        await new RoleSettingBuilder().ForRole(TestPlatform.Role("friends")).WithPurpose(RoleSettingPurpose.MemberAccess).SeedAsync(db, ids, Ct);

        (await db.RoleSettings.CountAsync(Ct)).ShouldBe(2);
    }

    [Fact]
    public async Task RoleSettings_WhenARowIsInserted_ShouldWriteAnAddedChangeWithItsAuthorAndTime()
    {
        await using var db = Fixture.CreateDbContext();
        var ids = Resolver(db);
        var author = await new UserBuilder().For(TestPlatform.User("owner")).SeedAsync(db, ids, Ct);

        var setting = await new RoleSettingBuilder().ChangedBy(author.Id).ChangedAt(TestTime.Now).SeedAsync(db, ids, Ct);

        var change = await db.RoleSettingChanges.AsNoTracking().SingleAsync(Ct);
        change.ShouldSatisfyAllConditions(
            () => change.GuildId.ShouldBe(setting.GuildId),
            () => change.Platform.ShouldBe(setting.Platform),
            () => change.Purpose.ShouldBe(RoleSettingPurpose.Tracking),
            () => change.RoleId.ShouldBe(setting.RoleId),
            () => change.Action.ShouldBe(RoleSettingAction.Added),
            () => change.ChangedByUserId.ShouldBe(author.Id),
            () => change.ChangedAt.ShouldBe(TestTime.Now));
    }

    [Fact]
    public async Task RoleSettings_WhenARowIsDeleted_ShouldWriteARemovedChange()
    {
        await using var db = Fixture.CreateDbContext();
        var setting = await new RoleSettingBuilder().SeedAsync(db, Resolver(db), Ct);

        db.RoleSettings.Remove(setting);
        await db.SaveChangesAsync(Ct);

        var changes = await db.RoleSettingChanges.AsNoTracking().OrderBy(c => c.Id).ToListAsync(Ct);
        changes.Select(c => c.Action).ShouldBe([RoleSettingAction.Added, RoleSettingAction.Removed]);
        changes[1].RoleId.ShouldBe(setting.RoleId);
        changes[1].ChangedByUserId.ShouldBeNull();
    }

    [Fact]
    public async Task RoleSettings_WhenTheInsertingTransactionRollsBack_ShouldLeaveNoChange()
    {
        await using var db = Fixture.CreateDbContext();
        var ids = Resolver(db);
        await ids.ResolveRoleAsync(TestPlatform.Guild, TestPlatform.Role("statistik"), Ct);

        await using (await db.Database.BeginTransactionAsync(Ct))
        {
            await new RoleSettingBuilder().SeedAsync(db, ids, Ct);
            (await db.RoleSettingChanges.CountAsync(Ct)).ShouldBe(1);
        }

        (await db.RoleSettingChanges.CountAsync(Ct)).ShouldBe(0);
    }

    [Fact]
    public async Task RoleSettings_WhenTheRoleOfARowChanges_ShouldWriteRemovedThenAdded()
    {
        await using var db = Fixture.CreateDbContext();
        var ids = Resolver(db);
        var setting = await new RoleSettingBuilder().SeedAsync(db, ids, Ct);
        var other = await ids.ResolveRoleAsync(TestPlatform.Guild, TestPlatform.Role("other"), Ct);

        await db.Database.ExecuteSqlAsync($"UPDATE role_settings SET role_id = {other} WHERE role_id = {setting.RoleId}", Ct);

        var changes = await db.RoleSettingChanges.AsNoTracking().OrderBy(c => c.Id).ToListAsync(Ct);
        changes.Select(c => (c.Action, c.RoleId)).ShouldBe(
            [(RoleSettingAction.Added, setting.RoleId), (RoleSettingAction.Removed, setting.RoleId), (RoleSettingAction.Added, other)]);
    }

    [Fact]
    public async Task TrackingPeriods_WhenASecondOpenPeriodIsInsertedForTheSameMember_ShouldBeRejected()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, userId) = await SeedMemberAsync(db);
        db.TrackingPeriods.Add(new TrackingPeriod { GuildId = guildId, UserId = userId, From = TestTime.Now.AddDays(-2), To = TestTime.Now.AddDays(-1) });
        db.TrackingPeriods.Add(new TrackingPeriod { GuildId = guildId, UserId = userId, From = TestTime.Now });
        await db.SaveChangesAsync(Ct);

        db.TrackingPeriods.Add(new TrackingPeriod { GuildId = guildId, UserId = userId, From = TestTime.Now.AddHours(1) });

        (await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(Ct))).InnerException!.Message.ShouldContain("duplicate key");
    }

    private static PlatformIdResolver Resolver(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);

    private static async Task<(Guid GuildId, Guid UserId)> SeedMemberAsync(ApolloDbContext db)
    {
        var ids = Resolver(db);
        return (await ids.ResolveGuildAsync(TestPlatform.Guild, Ct), await ids.ResolveUserAsync(TestPlatform.User("alice"), Ct));
    }
}
