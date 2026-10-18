using System.Globalization;
using Apollo.Domain.Voice;
using Apollo.Stats.Raw;
using Apollo.Testing.Data;

namespace Apollo.Stats.Tests.Raw;

public sealed class LongestVoiceSessionTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetLongestVoiceSessionAsync_ShouldReturnTheLongestClosedSessionThatStartedInTheRange()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Voice(seed.Alice, seed.Lounge, "2026-01-11T19:00:00Z", "2026-01-11T20:00:00Z").Build(),
            // Runs past the end of the range: still counts whole, it started inside.
            seed.Voice(seed.Bob, seed.Lobby, "2026-01-14T21:00:00Z", "2026-01-15T00:00:00Z").Build(),
            seed.Voice(seed.Carol, seed.Lounge, "2026-01-11T10:00:00Z", null).Build(), // open: ignored
            seed.Voice(seed.Carol, seed.Lounge, "2026-01-09T12:00:00Z", "2026-01-10T12:00:00Z").Build()); // started before

        var longest = await new RawStatsService(db).GetLongestVoiceSessionAsync(seed.Query(), Ct);

        longest.ShouldBe(new LongestVoiceSession(seed.Bob, seed.Lobby, Utc("2026-01-14T21:00:00Z"), TimeSpan.FromHours(3)));
    }

    [Fact]
    public async Task GetLongestVoiceSessionAsync_ShouldIncludeUnobservedEnds()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Voice(seed.Alice, seed.Lounge, "2026-01-11T19:00:00Z", null)
                .Until(Utc("2026-01-11T21:00:00Z"), VoiceSessionEndReason.Unobserved).Build());

        var longest = await new RawStatsService(db).GetLongestVoiceSessionAsync(seed.Query(), Ct);

        longest.ShouldNotBeNull().Duration.ShouldBe(TimeSpan.FromHours(2));
    }

    [Fact]
    public async Task GetLongestVoiceSessionAsync_WithoutClosedSessions_ShouldBeNull()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, seed.Voice(seed.Alice, seed.Lounge, "2026-01-11T19:00:00Z", null).Build());

        var longest = await new RawStatsService(db).GetLongestVoiceSessionAsync(seed.Query(), Ct);

        longest.ShouldBeNull();
    }

    [Fact]
    public async Task GetLongestVoiceSessionAsync_ForAUser_ShouldReturnTheirLongestSession()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Voice(seed.Alice, seed.Lounge, "2026-01-11T19:00:00Z", "2026-01-11T20:00:00Z").Build(),
            seed.Voice(seed.Bob, seed.Lounge, "2026-01-11T19:00:00Z", "2026-01-11T23:00:00Z").Build());

        var longest = await new RawStatsService(db).GetLongestVoiceSessionAsync(seed.Query(seed.Alice), Ct);

        longest.ShouldBe(new LongestVoiceSession(seed.Alice, seed.Lounge, Utc("2026-01-11T19:00:00Z"), TimeSpan.FromHours(1)));
    }

    [Fact]
    public async Task GetLongestVoiceSessionAsync_ShouldLeaveOutBotsUnlessIncluded()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Voice(seed.Alice, seed.Lounge, "2026-01-11T19:00:00Z", "2026-01-11T20:00:00Z").Build(),
            seed.Voice(seed.Bot, seed.Lounge, "2026-01-11T10:00:00Z", "2026-01-11T20:00:00Z").Build());

        var service = new RawStatsService(db);
        var withoutBots = await service.GetLongestVoiceSessionAsync(seed.Query(), Ct);
        var withBots = await service.GetLongestVoiceSessionAsync(seed.Query(includeBots: true), Ct);

        (withoutBots?.UserId, withBots?.UserId).ShouldBe((seed.Alice, seed.Bot));
    }

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
