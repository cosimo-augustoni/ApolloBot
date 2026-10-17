using Apollo.Bot.Tracking;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Tracking;
using Apollo.Testing;
using Apollo.Testing.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.Bot.Tests.Tracking;

public sealed class TrackingHistoryTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly DateTimeOffset _from = TestTime.Now.AddHours(-2);
    private static readonly DateTimeOffset _to = TestTime.Now.AddHours(-1);

    [Theory]
    [InlineData(-90, true)]
    [InlineData(-120, true)]
    [InlineData(-60, true)]
    [InlineData(-121, false)]
    [InlineData(-59, false)]
    public async Task WasTrackedAtAsync_ForAClosedPeriod_ShouldIncludeBothEnds(int minutesFromNow, bool expected)
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, userId) = await SeedPeriodAsync(db, _to);

        var tracked = await new TrackingHistory(db).WasTrackedAtAsync(
            guildId, userId, TestTime.Now.AddMinutes(minutesFromNow), TestContext.Current.CancellationToken);

        tracked.ShouldBe(expected);
    }

    [Fact]
    public async Task WasTrackedAtAsync_ForAnOpenPeriod_ShouldCoverEverythingFromItsStart()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, userId) = await SeedPeriodAsync(db, to: null);

        var tracked = await new TrackingHistory(db).WasTrackedAtAsync(
            guildId, userId, TestTime.Now.AddDays(1), TestContext.Current.CancellationToken);

        tracked.ShouldBeTrue();
    }

    [Fact]
    public async Task WasTrackedAtAsync_ForAnotherUser_ShouldBeFalse()
    {
        await using var db = Fixture.CreateDbContext();
        var (guildId, _) = await SeedPeriodAsync(db, to: null);

        var tracked = await new TrackingHistory(db).WasTrackedAtAsync(
            guildId, Guid.NewGuid(), TestTime.Now, TestContext.Current.CancellationToken);

        tracked.ShouldBeFalse();
    }

    private static async Task<(Guid GuildId, Guid UserId)> SeedPeriodAsync(ApolloDbContext db, DateTimeOffset? to)
    {
        var ct = TestContext.Current.CancellationToken;
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var guild = await new GuildBuilder().SeedAsync(db, ids, ct);
        var user = await new UserBuilder().SeedAsync(db, ids, ct);
        db.TrackingPeriods.Add(new TrackingPeriod { GuildId = guild.Id, UserId = user.Id, From = _from, To = to });
        await db.SaveChangesAsync(ct);
        return (guild.Id, user.Id);
    }
}
