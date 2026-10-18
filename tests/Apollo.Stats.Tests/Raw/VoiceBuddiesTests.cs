using Apollo.Stats.Raw;
using Apollo.Testing.Data;

namespace Apollo.Stats.Tests.Raw;

public sealed class VoiceBuddiesTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetVoiceBuddiesAsync_WhenSessionsOverlapInTheSameChannel_ShouldReturnTheOverlap()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Voice(seed.Alice, seed.Lounge, "2026-01-12T19:00:00Z", "2026-01-12T20:00:00Z").Build(), // 20:00–21:00
            seed.Voice(seed.Bob, seed.Lounge, "2026-01-12T19:30:00Z", "2026-01-12T21:00:00Z").Build(), // 20:30–22:00
            seed.Voice(seed.Carol, seed.Lobby, "2026-01-12T19:00:00Z", "2026-01-12T21:00:00Z").Build()); // another channel

        var buddies = await new RawStatsService(db).GetVoiceBuddiesAsync(seed.Query(), Ct);

        Pairs(buddies).ShouldBe([(Pair(seed.Alice, seed.Bob), 1800L)]);
    }

    [Fact]
    public async Task GetVoiceBuddiesAsync_ShouldOnlyCountTheOverlapInsideTheRange()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            // 9 Jan 23:00 to 10 Jan 02:00 and 14 Jan 23:00 to 15 Jan 02:00 (CET): two hours and one hour inside.
            seed.Voice(seed.Alice, seed.Lounge, "2026-01-09T22:00:00Z", "2026-01-10T01:00:00Z").Build(),
            seed.Voice(seed.Bob, seed.Lounge, "2026-01-09T22:00:00Z", "2026-01-10T01:00:00Z").Build(),
            seed.Voice(seed.Alice, seed.Lounge, "2026-01-14T22:00:00Z", "2026-01-15T01:00:00Z").Build(),
            seed.Voice(seed.Bob, seed.Lounge, "2026-01-14T22:00:00Z", "2026-01-15T01:00:00Z").Build());

        var buddies = await new RawStatsService(db).GetVoiceBuddiesAsync(seed.Query(), Ct);

        Pairs(buddies).ShouldBe([(Pair(seed.Alice, seed.Bob), 3 * 3600L)]);
    }

    [Fact]
    public async Task GetVoiceBuddiesAsync_ShouldIgnoreOpenSessions()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Voice(seed.Alice, seed.Lounge, "2026-01-12T19:00:00Z", "2026-01-12T20:00:00Z").Build(),
            seed.Voice(seed.Bob, seed.Lounge, "2026-01-12T19:00:00Z", null).Build());

        var buddies = await new RawStatsService(db).GetVoiceBuddiesAsync(seed.Query(), Ct);

        buddies.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetVoiceBuddiesAsync_ShouldSumEveryOverlapOfAPairAndReturnTheTopPairsFirst()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Voice(seed.Alice, seed.Lounge, "2026-01-11T19:00:00Z", "2026-01-11T20:00:00Z").Build(),
            seed.Voice(seed.Bob, seed.Lounge, "2026-01-11T19:00:00Z", "2026-01-11T20:00:00Z").Build(),
            seed.Voice(seed.Alice, seed.Lobby, "2026-01-12T19:00:00Z", "2026-01-12T19:30:00Z").Build(),
            seed.Voice(seed.Bob, seed.Lobby, "2026-01-12T19:00:00Z", "2026-01-12T19:20:00Z").Build(),
            seed.Voice(seed.Carol, seed.Lobby, "2026-01-12T19:00:00Z", "2026-01-12T19:10:00Z").Build());

        var buddies = await new RawStatsService(db).GetVoiceBuddiesAsync(seed.Query(top: 2), Ct);

        // Alice–Bob 60 + 20 min, Alice–Carol and Bob–Carol 10 min each (Carol's pairs tie; the top 2 keeps one).
        Pairs(buddies)[0].ShouldBe((Pair(seed.Alice, seed.Bob), 4800L));
        buddies.Count.ShouldBe(2);
        buddies[1].OverlapSeconds.ShouldBe(600);
    }

    [Fact]
    public async Task GetVoiceBuddiesAsync_ForAUser_ShouldReturnOnlyTheirBuddies()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Voice(seed.Alice, seed.Lounge, "2026-01-12T19:00:00Z", "2026-01-12T19:30:00Z").Build(),
            seed.Voice(seed.Bob, seed.Lounge, "2026-01-12T19:00:00Z", "2026-01-12T21:00:00Z").Build(),
            seed.Voice(seed.Carol, seed.Lounge, "2026-01-12T20:00:00Z", "2026-01-12T21:00:00Z").Build());

        var service = new RawStatsService(db);
        var alice = await service.GetVoiceBuddiesAsync(seed.Query(seed.Alice), Ct);
        var bob = await service.GetVoiceBuddiesAsync(seed.Query(seed.Bob), Ct);

        alice.ShouldBe([new VoiceBuddy(seed.Alice, seed.Bob, 1800)]);
        bob.ShouldBe([new VoiceBuddy(seed.Bob, seed.Carol, 3600), new VoiceBuddy(seed.Bob, seed.Alice, 1800)]);
    }

    [Fact]
    public async Task GetVoiceBuddiesAsync_ShouldLeaveOutBotsUnlessIncluded()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Voice(seed.Alice, seed.Lounge, "2026-01-12T19:00:00Z", "2026-01-12T20:00:00Z").Build(),
            seed.Voice(seed.Bot, seed.Lounge, "2026-01-12T19:00:00Z", "2026-01-12T20:00:00Z").Build());

        var service = new RawStatsService(db);
        var withoutBots = await service.GetVoiceBuddiesAsync(seed.Query(), Ct);
        var withBots = await service.GetVoiceBuddiesAsync(seed.Query(includeBots: true), Ct);

        withoutBots.ShouldBeEmpty();
        Pairs(withBots).ShouldBe([(Pair(seed.Alice, seed.Bot), 3600L)]);
    }

    private static (Guid, Guid) Pair(Guid a, Guid b) => a.CompareTo(b) < 0 ? (a, b) : (b, a);

    private static List<((Guid, Guid) Pair, long Seconds)> Pairs(IEnumerable<VoiceBuddy> buddies) =>
        [.. buddies.Select(b => (Pair(b.UserId, b.BuddyId), b.OverlapSeconds))];
}
