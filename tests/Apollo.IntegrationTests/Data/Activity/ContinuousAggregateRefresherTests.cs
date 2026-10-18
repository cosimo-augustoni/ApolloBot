using System.Globalization;
using Apollo.Data;
using Apollo.Data.Activity;
using Apollo.Data.Identity;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests.Data.Activity;

/// <summary>
/// <see cref="ContinuousAggregateRefresher"/> (#39): re-materializes whole Zurich days, and nothing outside them. Every
/// test seeds its own guild and reads only that guild's rows (aggregates are not reset).
/// </summary>
public sealed class ContinuousAggregateRefresherTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>
    /// Messages at the first and last hour of a Zurich day and on the days around it are materialized, then all of them
    /// are deleted from the raw table. Refreshing one instant of the day must drop the whole day from every aggregate,
    /// DST days included, and leave the neighbouring days as they were materialized. All days lie in the past of the
    /// container's clock: materializing future buckets would move the shared watermark past now and break the tests that
    /// rely on real-time aggregation.
    /// </summary>
    [Theory]
    [InlineData("2026-01-14T23:00:00Z", "2026-01-15T23:00:00Z")] // a 24 h winter day
    [InlineData("2025-03-29T23:00:00Z", "2025-03-30T22:00:00Z")] // spring forward: 23 h
    [InlineData("2025-10-25T22:00:00Z", "2025-10-26T23:00:00Z")] // fall back: 25 h
    public async Task RefreshAsync_ShouldRematerializeTheWholeZurichDaysOfTheRangeAndNothingElse(string dayStartUtc, string nextDayStartUtc)
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        var dayStart = Utc(dayStartUtc);
        var nextDayStart = Utc(nextDayStartUtc);
        var sentAt = new[]
        {
            dayStart.AddMinutes(-30), // the day before
            dayStart.AddMinutes(10),
            nextDayStart.AddMinutes(-10),
            nextDayStart.AddMinutes(30), // the day after
        };
        db.MessageEvents.AddRange(sentAt.Select(at => new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At(at).Build()));
        await db.SaveChangesAsync(Ct);
        await Fixture.RefreshContinuousAggregatesAsync(Ct);
        await db.MessageEvents.Where(e => e.GuildId == seed.GuildId).ExecuteDeleteAsync(Ct);

        var midday = dayStart.AddHours(12);
        await new ContinuousAggregateRefresher(db, NullLogger<ContinuousAggregateRefresher>.Instance).RefreshAsync(midday, midday, Ct);

        var days = await db.DailyUserChannelActivity.Where(a => a.GuildId == seed.GuildId).Select(a => a.Day).OrderBy(d => d).ToListAsync(Ct);
        days.ShouldBe([dayStart.AddDays(-1), nextDayStart]);
        var hours = await db.HourlyActivityHeatmap.Where(h => h.GuildId == seed.GuildId).Select(h => h.Hour).OrderBy(h => h).ToListAsync(Ct);
        hours.ShouldBe([dayStart.AddHours(-1), nextDayStart]);
    }

    [Fact]
    public async Task RefreshAsync_WhenATransactionIsOpen_ShouldThrowBeforeRefreshing()
    {
        await using var db = Fixture.CreateDbContext();
        await using var transaction = await db.Database.BeginTransactionAsync(Ct);

        var refresh = () => new ContinuousAggregateRefresher(db, NullLogger<ContinuousAggregateRefresher>.Instance).RefreshAsync(Utc("2026-01-15T00:00:00Z"), Utc("2026-01-15T00:00:00Z"), Ct);

        (await refresh.ShouldThrowAsync<InvalidOperationException>()).Message.ShouldContain("after the commit");
    }

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static async Task<Seed> SeedAsync(ApolloDbContext db)
    {
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var guild = await new GuildBuilder().SeedAsync(db, ids, Ct);
        var alice = await new GuildMemberBuilder().For(TestPlatform.User("alice")).SeedAsync(db, ids, Ct);
        var general = await new ChannelBuilder().For(TestPlatform.Channel("general")).Named("general").SeedAsync(db, ids, Ct);
        return new Seed(guild.Id, alice.UserId, general.Id);
    }

    private sealed record Seed(Guid GuildId, Guid Alice, Guid General);
}
