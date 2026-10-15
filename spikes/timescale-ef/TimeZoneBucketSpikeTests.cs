using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Apollo.Spikes.TimescaleEf;

public sealed class TimeZoneBucketSpikeTests(TimescaleFixture fixture) : IClassFixture<TimescaleFixture>
{
    private static readonly TimeZoneInfo _zurich = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Migrate_WhenDatabaseIsEmpty_ShouldCreateHypertableWithConfiguredChunkInterval()
    {
        await using SpikeDbContext db = fixture.CreateContext();

        TimeSpan chunkInterval = await db.Database
            .SqlQueryRaw<TimeSpan>(
                """
                SELECT d.time_interval AS "Value"
                FROM timescaledb_information.hypertables h
                JOIN timescaledb_information.dimensions d USING (hypertable_schema, hypertable_name)
                WHERE h.hypertable_name = 'activity_events' AND d.column_name = 'occurred_at'
                """)
            .SingleAsync(Ct);

        chunkInterval.ShouldBe(TimeSpan.FromDays(7));
    }

    [Fact]
    public async Task Migrate_WhenDatabaseDoesNotExist_ShouldCreateTimescaleExtension()
    {
        await using SpikeDbContext db = fixture.CreateContext();

        string version = await db.Database
            .SqlQueryRaw<string>("SELECT extversion AS \"Value\" FROM pg_extension WHERE extname = 'timescaledb'")
            .SingleAsync(Ct);

        version.ShouldBe("2.30.2");
    }

    [Fact]
    public async Task Model_WhenAllMigrationsAreApplied_ShouldHaveNoPendingChanges()
    {
        await using SpikeDbContext db = fixture.CreateContext();

        IEnumerable<string> pendingMigrations = await db.Database.GetPendingMigrationsAsync(Ct);

        db.Database.HasPendingModelChanges().ShouldBeFalse();
        pendingMigrations.ShouldBeEmpty();
    }

    [Fact]
    public async Task Hypertable_WhenUniqueIndexLacksPartitionColumn_ShouldRejectIt()
    {
        await using SpikeDbContext db = fixture.CreateContext();

        PostgresException ex = await Should.ThrowAsync<PostgresException>(() =>
            db.Database.ExecuteSqlRawAsync("CREATE UNIQUE INDEX ix_activity_events_id_only ON activity_events (id);", Ct));

        ex.MessageText.ShouldContain("occurred_at");
    }

    [Theory]
    [InlineData(1, "2026-01-14", "2026-01-13T23:00:00Z")]
    [InlineData(2, "2026-01-15", "2026-01-14T23:00:00Z")]
    [InlineData(3, "2026-03-29", "2026-03-28T23:00:00Z")]
    [InlineData(4, "2026-03-29", "2026-03-28T23:00:00Z")]
    [InlineData(5, "2026-03-30", "2026-03-29T22:00:00Z")]
    [InlineData(6, "2026-10-25", "2026-10-24T22:00:00Z")]
    [InlineData(7, "2026-10-25", "2026-10-24T22:00:00Z")]
    [InlineData(8, "2026-10-25", "2026-10-24T22:00:00Z")]
    [InlineData(9, "2026-10-25", "2026-10-24T22:00:00Z")]
    [InlineData(10, "2026-10-26", "2026-10-25T23:00:00Z")]
    public async Task ZurichAggregate_WhenRefreshed_ShouldCountEventInItsZurichCalendarDay(
        long channelId, string expectedZurichDay, string expectedBucketUtc)
    {
        await using SpikeDbContext db = fixture.CreateContext();

        DailyActivityZurich row = await db.DailyActivityZurich.SingleAsync(x => x.ChannelId == channelId, Ct);

        row.EventCount.ShouldBe(1);
        row.Day.ShouldBe(DateTimeOffset.Parse(expectedBucketUtc, CultureInfo.InvariantCulture));
        TimeZoneInfo.ConvertTime(row.Day, _zurich).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            .ShouldBe($"{expectedZurichDay} 00:00");
    }

    [Theory]
    [InlineData("2026-03-28T23:00:00Z", "2026-03-29T22:00:00Z", 2)] // spring forward: 23 h day
    [InlineData("2026-10-24T22:00:00Z", "2026-10-25T23:00:00Z", 4)] // fall back: 25 h day
    public async Task ZurichAggregate_OnDstSwitchDay_ShouldCountTheWholeLocalDayInOneBucket(
        string dayStartUtc, string nextDayStartUtc, long expectedEvents)
    {
        var dayStart = DateTimeOffset.Parse(dayStartUtc, CultureInfo.InvariantCulture);
        var nextDayStart = DateTimeOffset.Parse(nextDayStartUtc, CultureInfo.InvariantCulture);
        await using SpikeDbContext db = fixture.CreateContext();

        long events = await db.DailyActivityZurich.Where(x => x.Day == dayStart).SumAsync(x => x.EventCount, Ct);
        bool hasBucketInsideTheDay = await db.DailyActivityZurich.AnyAsync(x => x.Day > dayStart && x.Day < nextDayStart, Ct);

        events.ShouldBe(expectedEvents);
        hasBucketInsideTheDay.ShouldBeFalse();
    }

    [Theory]
    [InlineData(1, "2026-01-14T00:00:00Z")]
    [InlineData(2, "2026-01-14T00:00:00Z")] // Zurich 15 Jan, but the UTC bucket says 14 Jan
    [InlineData(5, "2026-03-29T00:00:00Z")] // Zurich 30 Mar, but the UTC bucket says 29 Mar
    public async Task FluentUtcAggregate_WhenRefreshed_ShouldBucketByUtcDayNotZurichDay(long channelId, string expectedBucketUtc)
    {
        await using SpikeDbContext db = fixture.CreateContext();

        DailyActivityUtc row = await db.DailyActivityUtc.SingleAsync(x => x.ChannelId == channelId, Ct);

        row.Day.ShouldBe(DateTimeOffset.Parse(expectedBucketUtc, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task ZurichAggregate_ViewDefinition_ShouldUseTimeZoneAwareTimeBucket()
    {
        await using SpikeDbContext db = fixture.CreateContext();

        string definition = await db.Database
            .SqlQueryRaw<string>(
                "SELECT view_definition AS \"Value\" FROM timescaledb_information.continuous_aggregates WHERE view_name = 'daily_activity_zurich'")
            .SingleAsync(Ct);

        definition.ShouldContain("'Europe/Zurich'");
    }

    [Theory]
    [InlineData("daily_activity_utc")]
    [InlineData("daily_activity_zurich")]
    public async Task RefreshPolicy_WhenMigrated_ShouldBeScheduledForAggregate(string viewName)
    {
        await using SpikeDbContext db = fixture.CreateContext();

        TimeSpan scheduleInterval = await db.Database
            .SqlQueryRaw<TimeSpan>(
                """
                SELECT schedule_interval AS "Value"
                FROM timescaledb_information.jobs
                WHERE proc_name = 'policy_refresh_continuous_aggregate' AND hypertable_name = {0}
                """,
                viewName)
            .SingleAsync(Ct);

        scheduleInterval.ShouldBe(TimeSpan.FromHours(1));
    }

    [Theory]
    [InlineData("policy_compression")]
    [InlineData("policy_retention")]
    public async Task HypertablePolicy_WhenMigrated_ShouldBeScheduled(string procName)
    {
        await using SpikeDbContext db = fixture.CreateContext();

        int jobs = await db.Database
            .SqlQueryRaw<int>(
                "SELECT count(*)::int AS \"Value\" FROM timescaledb_information.jobs WHERE hypertable_name = 'activity_events' AND proc_name = {0}",
                procName)
            .SingleAsync(Ct);

        jobs.ShouldBe(1);
    }

    [Fact]
    public async Task ManualRefresh_WhenNewEventArrives_ShouldMaterializeItOnlyAfterRefresh()
    {
        const long ChannelId = 100;
        await using SpikeDbContext db = fixture.CreateContext();
        db.ActivityEvents.Add(new ActivityEvent
        {
            ChannelId = ChannelId,
            OccurredAt = DateTimeOffset.Parse("2026-02-10T12:00:00Z", CultureInfo.InvariantCulture),
        });
        await db.SaveChangesAsync(Ct);

        bool visibleBeforeRefresh = await db.DailyActivityZurich.AnyAsync(x => x.ChannelId == ChannelId, Ct);
        await db.Database.ExecuteSqlRawAsync(
            "CALL refresh_continuous_aggregate('daily_activity_zurich', '2026-02-09T23:00:00Z'::timestamptz, '2026-02-10T23:00:00Z'::timestamptz);",
            Ct);
        bool visibleAfterRefresh = await db.DailyActivityZurich.AnyAsync(x => x.ChannelId == ChannelId, Ct);

        visibleBeforeRefresh.ShouldBeFalse();
        visibleAfterRefresh.ShouldBeTrue();
    }

    [Fact]
    public async Task Versions_WhenRunning_ShouldBeTheOnesRecordedInTheAdr()
    {
        await using SpikeDbContext db = fixture.CreateContext();

        string server = await db.Database.SqlQueryRaw<string>("SELECT version() AS \"Value\"").SingleAsync(Ct);
        string efCore = InformationalVersion(typeof(DbContext));
        string npgsqlProvider = InformationalVersion(typeof(NpgsqlDbContextOptionsBuilderExtensions));
        string npgsql = InformationalVersion(typeof(NpgsqlConnection));
        string cmdScale = InformationalVersion(typeof(CmdScale.EntityFrameworkCore.TimescaleDB.TimescaleDbContextOptionsBuilderExtensions));
        TestContext.Current.TestOutputHelper?.WriteLine(
            "EF Core {0} | Npgsql EF provider {1} | Npgsql {2} | CmdScale {3} | {4}", efCore, npgsqlProvider, npgsql, cmdScale, server);

        efCore.ShouldStartWith("10.0.13");
        npgsqlProvider.ShouldStartWith("10.0.3");
        // CmdScale stamps its assemblies 1.0.0+<commit>; this commit is the one the 10.4.0 nuspec points to.
        cmdScale.ShouldEndWith("+0c1f648944f67bbe47b2d390946713b1c2081c53");
        server.ShouldStartWith("PostgreSQL 18.");
    }

    private static string InformationalVersion(Type type) =>
        type.Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;
}
