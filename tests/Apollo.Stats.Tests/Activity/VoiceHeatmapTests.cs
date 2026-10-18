using System.Globalization;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Voice;
using Apollo.Stats.Activity;
using Apollo.Stats.Raw;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.Stats.Tests.Activity;

/// <summary>
/// <see cref="ActivityStatsDto.VoiceHeatmap"/> (#111): closed voice sessions split at the Zurich hour boundaries and
/// clipped to the range. Times in the comments are Zurich time.
/// </summary>
public sealed class VoiceHeatmapTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const int Monday = 0;
    private const int Tuesday = 1;
    private const int Sunday = 6;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_WithASessionOverSeveralHours_ShouldSplitItAtTheHourBoundaries()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        // Tuesday 13 Jan 21:40 to 23:10 CET.
        await AddAsync(db, Session(seed, seed.Alice, "2026-01-13T20:40:00Z", "2026-01-13T22:10:00Z"));

        var heatmap = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, Date("2026-01-12"), Date("2026-01-18")));

        (heatmap[Tuesday][21], heatmap[Tuesday][22], heatmap[Tuesday][23]).ShouldBe((20 * 60L, 60 * 60L, 10 * 60L));
        heatmap.Sum(day => day.Sum()).ShouldBe(90 * 60);
    }

    [Fact]
    public async Task GetAsync_ShouldReturnASevenByTwentyFourHeatmap()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);

        var heatmap = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, Date("2026-01-12"), Date("2026-01-18")));

        heatmap.Count.ShouldBe(7);
        heatmap.ShouldAllBe(day => day.Count == 24 && day.All(seconds => seconds == 0));
    }

    [Fact]
    public async Task GetAsync_WithASessionAcrossMidnight_ShouldSplitItAcrossBothWeekdays()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        // Sunday 11 Jan 23:30 to Monday 12 Jan 00:45 CET.
        await AddAsync(db, Session(seed, seed.Alice, "2026-01-11T22:30:00Z", "2026-01-11T23:45:00Z"));

        var heatmap = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, Date("2026-01-05"), Date("2026-01-18")));

        (heatmap[Sunday][23], heatmap[Monday][0]).ShouldBe((30 * 60L, 45 * 60L));
        heatmap.Sum(day => day.Sum()).ShouldBe(75 * 60);
    }

    [Fact]
    public async Task GetAsync_WithSessionsOverTheRangeEdges_ShouldCountOnlyTheMinutesInsideIt()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            // Sunday 11 Jan 23:30 to Monday 12 Jan 00:30 CET: starts before the range.
            Session(seed, seed.Alice, "2026-01-11T22:30:00Z", "2026-01-11T23:30:00Z"),
            // Tuesday 13 Jan 23:20 to Wednesday 14 Jan 01:00 CET: ends after it.
            Session(seed, seed.Bob, "2026-01-13T22:20:00Z", "2026-01-14T00:00:00Z"),
            // Saturday 10 Jan 20:00 to 21:00 CET: before the range.
            Session(seed, seed.Alice, "2026-01-10T19:00:00Z", "2026-01-10T20:00:00Z"));

        var heatmap = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, Date("2026-01-12"), Date("2026-01-13")));

        (heatmap[Sunday][23], heatmap[Monday][0], heatmap[Tuesday][23]).ShouldBe((0L, 30 * 60L, 40 * 60L));
        heatmap.Sum(day => day.Sum()).ShouldBe(70 * 60);
    }

    [Fact]
    public async Task GetAsync_OnTheSpringForwardDay_ShouldPutTheMinutesOnTheZurichHoursAndKeepTheRealLength()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        // Sunday 29 Mar 01:30 CET to 03:30 CEST: one real hour, as the clocks skip 02:00.
        await AddAsync(db, Session(seed, seed.Alice, "2026-03-29T00:30:00Z", "2026-03-29T01:30:00Z"));

        var heatmap = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, Date("2026-03-29"), Date("2026-03-29")));

        (heatmap[Sunday][1], heatmap[Sunday][2], heatmap[Sunday][3]).ShouldBe((30 * 60L, 0L, 30 * 60L));
        heatmap.Sum(day => day.Sum()).ShouldBe(60 * 60);
    }

    [Fact]
    public async Task GetAsync_OnTheFallBackDay_ShouldPutBothPassesOfTheRepeatedHourIntoTwoOClockAndKeepTheRealLength()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        // Sunday 25 Oct 01:30 CEST to 02:30 CET: two real hours, as the clocks pass 02:00 twice.
        await AddAsync(db, Session(seed, seed.Alice, "2026-10-24T23:30:00Z", "2026-10-25T01:30:00Z"));

        var heatmap = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, Date("2026-10-25"), Date("2026-10-25")));

        (heatmap[Sunday][1], heatmap[Sunday][2], heatmap[Sunday][3]).ShouldBe((30 * 60L, 90 * 60L, 0L));
        heatmap.Sum(day => day.Sum()).ShouldBe(120 * 60);
    }

    [Theory]
    [InlineData(false, 60L * 60)]
    [InlineData(true, 2L * 60 * 60)]
    public async Task GetAsync_WithABotSession_ShouldCountItOnlyWhenBotsAreIncluded(bool includeBots, long expectedSeconds)
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Session(seed, seed.Alice, "2026-01-13T18:00:00Z", "2026-01-13T19:00:00Z"),
            Session(seed, seed.Bot, "2026-01-13T18:00:00Z", "2026-01-13T19:00:00Z"));

        var heatmap = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, Date("2026-01-12"), Date("2026-01-18"), IncludeBots: includeBots));

        heatmap.Sum(day => day.Sum()).ShouldBe(expectedSeconds);
    }

    [Fact]
    public async Task GetAsync_WithASessionInAnotherGuild_ShouldNotCountIt()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var other = await new GuildBuilder().For(TestPlatform.User("other-guild")).SeedAsync(db, ids, Ct);
        await AddAsync(
            db,
            Session(seed, seed.Alice, "2026-01-13T18:00:00Z", "2026-01-13T19:00:00Z"),
            new VoiceSessionBuilder().In(other.Id, seed.Lounge).By(seed.Alice).From("2026-01-13T18:00:00Z").Until("2026-01-13T20:00:00Z").Build());

        var heatmap = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, Date("2026-01-12"), Date("2026-01-18")));

        heatmap.Sum(day => day.Sum()).ShouldBe(60 * 60);
    }

    [Fact]
    public async Task GetAsync_ForAUser_ShouldCountOnlyTheirSessions()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Session(seed, seed.Alice, "2026-01-13T18:00:00Z", "2026-01-13T18:30:00Z"),
            Session(seed, seed.Bob, "2026-01-13T18:00:00Z", "2026-01-13T20:00:00Z"));

        var heatmap = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, seed.Alice, Date("2026-01-12"), Date("2026-01-18")));

        heatmap[Tuesday][19].ShouldBe(30 * 60);
        heatmap.Sum(day => day.Sum()).ShouldBe(30 * 60);
    }

    [Fact]
    public async Task GetAsync_ShouldSumToTheVoiceSecondsOfTheClosedSessionsInsideTheRange()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        var range = StatsRange.ForZurichDays(Date("2026-01-12"), Date("2026-01-18"));
        VoiceSession[] closed =
        [
            Session(seed, seed.Alice, "2026-01-11T21:17:13Z", "2026-01-12T02:03:59Z"),
            Session(seed, seed.Bob, "2026-01-12T07:59:59Z", "2026-01-12T08:00:01Z"),
            Session(seed, seed.Alice, "2026-01-14T13:05:00Z", "2026-01-16T09:45:30Z"),
            Session(seed, seed.Bob, "2026-01-18T22:12:00Z", "2026-01-19T03:00:00Z"),
            Session(seed, seed.Bob, "2026-01-20T10:00:00Z", "2026-01-20T11:00:00Z"),
        ];
        await AddAsync(db, [.. closed, new VoiceSessionBuilder().In(seed.GuildId, seed.Lounge).By(seed.Alice).From("2026-01-15T10:00:00Z").Build()]);

        var heatmap = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, Date("2026-01-12"), Date("2026-01-18")));

        heatmap.Sum(day => day.Sum()).ShouldBe(closed.Sum(s => (long)range.Clip(s.OccurredAt, s.EndedAt!.Value).TotalSeconds));
    }

    private async Task<IReadOnlyList<IReadOnlyList<long>>> GetAsync(ApolloDbContext db, ActivityStatsQuery query)
    {
        await Fixture.RefreshContinuousAggregatesAsync(Ct);
        var stats = await new ActivityStatsService(db, TestTime.CreateProvider()).GetAsync(query, Ct);
        return stats.VoiceHeatmap;
    }

    private static VoiceSession Session(Seed seed, Guid userId, string fromUtc, string untilUtc) =>
        new VoiceSessionBuilder().In(seed.GuildId, seed.Lounge).By(userId).From(fromUtc).Until(untilUtc).Build();

    private static DateOnly Date(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture);

    private static async Task AddAsync(ApolloDbContext db, params object[] rows)
    {
        db.AddRange(rows);
        await db.SaveChangesAsync(Ct);
    }

    private static async Task<Seed> SeedAsync(ApolloDbContext db)
    {
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var guild = await new GuildBuilder().SeedAsync(db, ids, Ct);
        var alice = await new GuildMemberBuilder().For(TestPlatform.User("alice")).SeedAsync(db, ids, Ct);
        var bob = await new GuildMemberBuilder().For(TestPlatform.User("bob")).SeedAsync(db, ids, Ct);
        var bot = await new GuildMemberBuilder().For(TestPlatform.User("some-bot")).AsBot().SeedAsync(db, ids, Ct);
        var lounge = await new ChannelBuilder().For(TestPlatform.Channel("lounge")).Named("lounge").SeedAsync(db, ids, Ct);
        return new Seed(guild.Id, alice.UserId, bob.UserId, bot.UserId, lounge.Id);
    }

    private sealed record Seed(Guid GuildId, Guid Alice, Guid Bob, Guid Bot, Guid Lounge);
}
