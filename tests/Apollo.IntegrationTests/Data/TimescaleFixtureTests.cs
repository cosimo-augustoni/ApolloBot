using Apollo.Data.Identity;
using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests.Data;

/// <summary>
/// Proves the shared fixture end to end: migrated once per collection, reset before every test.
/// </summary>
public sealed class TimescaleFixtureTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    [Fact]
    public async Task Fixture_WhenCollectionStarts_ShouldApplyAllMigrations()
    {
        await using var db = Fixture.CreateDbContext();

        var applied = await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken);

        applied.ShouldBe(db.Database.GetMigrations());
        Fixture.AppliedMigrations.ShouldBe(db.Database.GetMigrations().ToList());
    }

    // Both rows insert one row into the same table and expect exactly one: whichever runs second only passes when the
    // reset before it removed the row of the first.
    [Theory]
    [InlineData("first")]
    [InlineData("second")]
    public async Task Reset_WhenPreviousTestWroteRows_ShouldStartEmpty(string writer)
    {
        await using var db = Fixture.CreateDbContext();
        await SampleEvents.CreateTableAsync(db);

        await SampleEvents.InsertAsync(db, writer);

        (await SampleEvents.ReadAllAsync(db)).ShouldBe([writer]);
    }

    [Fact]
    public async Task ResetAsync_WhenRowsExist_ShouldKeepMigrationHistoryAndTimescaleExtension()
    {
        await using var db = Fixture.CreateDbContext();
        await SampleEvents.CreateTableAsync(db);
        await SampleEvents.InsertAsync(db, "before reset");

        await Fixture.ResetAsync(TestContext.Current.CancellationToken);

        (await SampleEvents.ReadAllAsync(db)).ShouldBeEmpty();
        (await db.Database.GetAppliedMigrationsAsync(TestContext.Current.CancellationToken))
            .ShouldBe(db.Database.GetMigrations());
        (await db.Database.SqlQueryRaw<string>("SELECT extname AS \"Value\" FROM pg_extension")
                .ToListAsync(TestContext.Current.CancellationToken))
            .ShouldContain("timescaledb");
    }

    [Fact]
    public async Task ResetAsync_WhenRoleSettingsExist_ShouldLeaveNoAuditEntriesBehind()
    {
        await using (var db = Fixture.CreateDbContext())
        {
            var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
            await new RoleSettingBuilder().SeedAsync(db, ids, TestContext.Current.CancellationToken);
        }

        await Fixture.ResetAsync(TestContext.Current.CancellationToken);

        await using var read = Fixture.CreateDbContext();
        (await read.RoleSettingChanges.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(0);
    }

    /// <summary>
    /// A test-only hypertable. Its primary key includes the partition column, as every event table's must (ADR 0001).
    /// </summary>
    private static class SampleEvents
    {
        public static async Task CreateTableAsync(DbContext db)
        {
            var ct = TestContext.Current.CancellationToken;
            await db.Database.ExecuteSqlRawAsync(
                """
                CREATE TABLE IF NOT EXISTS sample_events (
                    id bigint GENERATED ALWAYS AS IDENTITY,
                    occurred_at timestamptz NOT NULL,
                    writer text NOT NULL,
                    PRIMARY KEY (id, occurred_at));
                SELECT create_hypertable('sample_events', 'occurred_at', if_not_exists => TRUE);
                """,
                ct);
        }

        public static Task InsertAsync(DbContext db, string writer) =>
            db.Database.ExecuteSqlAsync(
                $"INSERT INTO sample_events (occurred_at, writer) VALUES ('2026-01-15T09:00:00Z', {writer})",
                TestContext.Current.CancellationToken);

        public static Task<List<string>> ReadAllAsync(DbContext db) =>
            db.Database.SqlQueryRaw<string>("SELECT writer AS \"Value\" FROM sample_events ORDER BY id")
                .ToListAsync(TestContext.Current.CancellationToken);
    }
}
