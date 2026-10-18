using Apollo.Stats.Tests.Raw;
using Apollo.Stats.Sanity;
using Apollo.Testing.Data;

namespace Apollo.Stats.Tests.Sanity;

public sealed class HeartbeatGapTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_WhenHeartbeatsHaveOneFiveMinuteHole_ShouldReportExactlyThatGapWithItsStartAndEnd()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        var holeStart = SanitySeed.Utc("2026-01-12T10:00:00Z");
        var holeEnd = holeStart.AddMinutes(5);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            [.. seed.Heartbeats(holeStart.AddHours(-1), holeStart.AddSeconds(1)), .. seed.Heartbeats(holeEnd, holeEnd.AddHours(1))]);

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.HeartbeatGaps.Count.ShouldBe(1);
        report.HeartbeatGaps.Examples.ShouldBe([new HeartbeatGap(holeStart, holeEnd)]);
    }

    [Fact]
    public async Task GetAsync_WhenHeartbeatsAreAtMostTheThresholdApart_ShouldReportNoGap()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        var start = SanitySeed.Utc("2026-01-12T10:00:00Z");
        var last = start.AddHours(1).AddSeconds(-30);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            [.. seed.Heartbeats(start, start.AddHours(1)), seed.Heartbeat(last.AddMinutes(2))]);

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.HeartbeatGaps.Count.ShouldBe(0);
        report.HeartbeatGaps.Examples.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsync_WhenAGapStartsBeforeTheRange_ShouldReportItFromTheLastHeartbeatBefore()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        // The range is 2026-01-09T23:00Z (midnight in Zurich) to 2026-01-14T23:00Z.
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Heartbeat(SanitySeed.Utc("2026-01-08T12:00:00Z")),
            seed.Heartbeat(SanitySeed.Utc("2026-01-09T22:00:00Z")),
            seed.Heartbeat(SanitySeed.Utc("2026-01-10T08:00:00Z")),
            seed.Heartbeat(SanitySeed.Utc("2026-01-10T08:00:30Z")));

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.HeartbeatGaps.Examples.ShouldBe([new HeartbeatGap(SanitySeed.Utc("2026-01-09T22:00:00Z"), SanitySeed.Utc("2026-01-10T08:00:00Z"))]);
    }

    [Fact]
    public async Task GetAsync_WhenAGapEndsAfterTheRange_ShouldReportItUpToTheFirstHeartbeatAfter()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        // The range ends at 2026-01-14T23:00Z (midnight in Zurich).
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Heartbeat(SanitySeed.Utc("2026-01-14T22:00:00Z")),
            seed.Heartbeat(SanitySeed.Utc("2026-01-15T01:00:00Z")),
            seed.Heartbeat(SanitySeed.Utc("2026-01-15T08:00:00Z")));

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.HeartbeatGaps.Examples.ShouldBe([new HeartbeatGap(SanitySeed.Utc("2026-01-14T22:00:00Z"), SanitySeed.Utc("2026-01-15T01:00:00Z"))]);
    }

    [Fact]
    public async Task GetAsync_ShouldUseTheThresholdOfTheQuery()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        var start = SanitySeed.Utc("2026-01-12T10:00:00Z");
        await RawStatsSeed.AddAsync(db, Ct, seed.Heartbeat(start), seed.Heartbeat(start.AddMinutes(4)));

        var lenient = await SanitySeed.Service(db).GetAsync(seed.Query() with { HeartbeatGapThreshold = TimeSpan.FromMinutes(5) }, Ct);
        var strict = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        (lenient.HeartbeatGaps.Count, strict.HeartbeatGaps.Count).ShouldBe((0, 1));
    }

    [Fact]
    public async Task GetAsync_WhenThereAreMoreGapsThanExamples_ShouldCountAllAndShowTheNewest()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        var start = SanitySeed.Utc("2026-01-12T00:00:00Z");
        await RawStatsSeed.AddAsync(db, Ct, [.. Enumerable.Range(0, 26).Select(i => seed.Heartbeat(start.AddMinutes(10 * i)))]);

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.HeartbeatGaps.Count.ShouldBe(25);
        report.HeartbeatGaps.Examples.Count.ShouldBe(DataSanityQuery.MaxExamples);
        report.HeartbeatGaps.Examples[0].ShouldBe(new HeartbeatGap(start.AddMinutes(240), start.AddMinutes(250)));
    }

    [Fact]
    public async Task GetAsync_ShouldIgnoreHeartbeatsOfOtherGuilds()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        var start = SanitySeed.Utc("2026-01-12T10:00:00Z");
        await RawStatsSeed.AddAsync(db, Ct, seed.Heartbeat(start), seed.Heartbeat(start.AddHours(1)));

        var report = await SanitySeed.Service(db).GetAsync(seed.Query() with { GuildId = Guid.NewGuid() }, Ct);

        report.HeartbeatGaps.Count.ShouldBe(0);
    }
}
