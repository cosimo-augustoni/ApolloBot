using Apollo.Domain.Presence;
using Apollo.Stats.Raw;
using Apollo.Testing.Data;

namespace Apollo.Stats.Tests.Raw;

public sealed class TopGamesTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetTopGamesAsync_ShouldSumTheClippedDurationOfPlayingSessionsPerName()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            [
                .. Game(seed, seed.Alice, "Factorio", "2026-01-11T18:00:00Z", "2026-01-11T20:00:00Z"),
                .. Game(seed, seed.Bob, "Factorio", "2026-01-12T18:00:00Z", "2026-01-12T19:00:00Z"),
                // 9 Jan 23:00 to 10 Jan 01:00 CET: one hour inside the range.
                .. Game(seed, seed.Alice, "Celeste", "2026-01-09T22:00:00Z", "2026-01-10T00:00:00Z"),
                .. Game(seed, seed.Alice, "Celeste", "2026-01-15T00:00:00Z", "2026-01-15T05:00:00Z"), // after the range
                .. new PresenceActivitySessionBuilder().In(seed.GuildId).By(seed.Alice).Doing(PresenceActivityType.Listening, "Spotify")
                    .From("2026-01-11T08:00:00Z").Until("2026-01-11T18:00:00Z").BuildRows().All,
            ]);

        var games = await new RawStatsService(db).GetTopGamesAsync(seed.Query(), Ct);

        games.ShouldBe([new GamePlaytime("Factorio", 3 * 3600), new GamePlaytime("Celeste", 3600)]);
    }

    [Fact]
    public async Task GetTopGamesAsync_WithoutPresenceData_ShouldBeEmpty()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);

        var games = await new RawStatsService(db).GetTopGamesAsync(seed.Query(), Ct);

        games.ShouldNotBeNull().ShouldBeEmpty();
    }

    [Fact]
    public async Task GetTopGamesAsync_ShouldReturnTheTopNWithTiesByName()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            [
                .. Game(seed, seed.Alice, "Zelda", "2026-01-11T18:00:00Z", "2026-01-11T19:00:00Z"),
                .. Game(seed, seed.Alice, "Celeste", "2026-01-11T19:00:00Z", "2026-01-11T20:00:00Z"),
                .. Game(seed, seed.Alice, "Factorio", "2026-01-11T20:00:00Z", "2026-01-11T20:30:00Z"),
            ]);

        var games = await new RawStatsService(db).GetTopGamesAsync(seed.Query(top: 2), Ct);

        games.ShouldBe([new GamePlaytime("Celeste", 3600), new GamePlaytime("Zelda", 3600)]);
    }

    [Fact]
    public async Task GetTopGamesAsync_ForAUser_ShouldSumOnlyTheirSessions()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            [
                .. Game(seed, seed.Alice, "Factorio", "2026-01-11T18:00:00Z", "2026-01-11T19:00:00Z"),
                .. Game(seed, seed.Bob, "Factorio", "2026-01-11T18:00:00Z", "2026-01-11T20:00:00Z"),
                .. Game(seed, seed.Bob, "Celeste", "2026-01-11T18:00:00Z", "2026-01-11T20:00:00Z"),
            ]);

        var games = await new RawStatsService(db).GetTopGamesAsync(seed.Query(seed.Alice), Ct);

        games.ShouldBe([new GamePlaytime("Factorio", 3600)]);
    }

    [Fact]
    public async Task GetTopGamesAsync_ShouldLeaveOutBotsUnlessIncluded()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, Game(seed, seed.Bot, "Factorio", "2026-01-11T18:00:00Z", "2026-01-11T19:00:00Z"));

        var service = new RawStatsService(db);
        var withoutBots = await service.GetTopGamesAsync(seed.Query(), Ct);
        var withBots = await service.GetTopGamesAsync(seed.Query(includeBots: true), Ct);

        (withoutBots.Count, withBots.Count).ShouldBe((0, 1));
    }

    private static object[] Game(RawStatsSeed seed, Guid userId, string name, string fromUtc, string untilUtc) =>
        new PresenceActivitySessionBuilder().In(seed.GuildId).By(userId).Playing(name).From(fromUtc).Until(untilUtc).BuildRows().All;
}
