using System.Globalization;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Tracking;
using Apollo.Stats.Tracking;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.Stats.Tests.Tracking;

/// <summary><see cref="TrackingStatusService"/> over <c>tracking_periods</c> (#38, scope §5.1.1).</summary>
public sealed class TrackingStatusServiceTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_WhenAPeriodIsOpen_ShouldBeTrackedSinceItsStartInZurich()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Period(seed, seed.Alice, "2025-11-01T10:00:00Z", "2025-11-20T10:00:00Z"),
            Period(seed, seed.Alice, "2026-02-02T23:30:00Z", null));

        var status = await new TrackingStatusService(db).GetAsync(seed.GuildId, seed.Alice, Ct);

        status.ShouldBe(new TrackingStatus(TrackedSince: new DateOnly(2026, 2, 3), TrackedUntil: new DateOnly(2025, 11, 20)));
        (status.IsTracked, status.WasEverTracked).ShouldBe((true, true));
    }

    [Fact]
    public async Task GetAsync_WhenEveryPeriodIsClosed_ShouldBeNotTrackedUntilTheEndOfTheLatest()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Period(seed, seed.Alice, "2025-11-01T10:00:00Z", "2025-11-20T10:00:00Z"),
            Period(seed, seed.Alice, "2026-01-05T10:00:00Z", "2026-01-11T23:15:00Z"));

        var status = await new TrackingStatusService(db).GetAsync(seed.GuildId, seed.Alice, Ct);

        status.ShouldBe(new TrackingStatus(TrackedSince: null, TrackedUntil: new DateOnly(2026, 1, 12)));
        (status.IsTracked, status.WasEverTracked).ShouldBe((false, true));
    }

    [Fact]
    public async Task GetAsync_WhenNeverTracked_ShouldBeNever()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(db, Period(seed, seed.Bob, "2026-01-05T10:00:00Z", null));

        var status = await new TrackingStatusService(db).GetAsync(seed.GuildId, seed.Alice, Ct);

        status.ShouldBe(TrackingStatus.Never);
        (status.IsTracked, status.WasEverTracked).ShouldBe((false, false));
    }

    [Fact]
    public async Task GetAsync_ShouldIgnorePeriodsInOtherGuilds()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var other = await new GuildBuilder().For(TestPlatform.User("other-guild")).SeedAsync(db, ids, Ct);
        await AddAsync(db, Period(seed, seed.Alice, "2026-01-05T10:00:00Z", null));

        var status = await new TrackingStatusService(db).GetAsync(other.Id, seed.Alice, Ct);

        status.ShouldBe(TrackingStatus.Never);
    }

    [Fact]
    public async Task AddTrackingStatus_ShouldResolveTheServiceOnTheRuntimeRegistration()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(db, Period(seed, seed.Alice, "2026-01-05T10:00:00Z", null));
        var builder = Fixture.CreateApplicationBuilder();
        builder.Services.AddTrackingStatus();
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();

        var status = await scope.ServiceProvider.GetRequiredService<ITrackingStatusService>().GetAsync(seed.GuildId, seed.Alice, Ct);

        status.IsTracked.ShouldBeTrue();
    }

    private static TrackingPeriod Period(Seed seed, Guid userId, string fromUtc, string? toUtc) => new()
    {
        GuildId = seed.GuildId,
        UserId = userId,
        From = Utc(fromUtc),
        To = toUtc is null ? null : Utc(toUtc),
    };

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

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
        return new Seed(guild.Id, alice.UserId, bob.UserId);
    }

    private sealed record Seed(Guid GuildId, Guid Alice, Guid Bob);
}
