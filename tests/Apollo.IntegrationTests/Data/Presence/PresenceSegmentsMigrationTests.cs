using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Presence;
using Apollo.Domain.Presence;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests.Data.Presence;

/// <summary>
/// #115 added <c>presence_activity_segments</c>; production already has sessions without segments.
/// </summary>
[Collection<TimescaleCollection>]
public sealed class PresenceSegmentsMigrationTests(TimescaleFixture fixture)
{
    private const string Migration = "_AddPresenceActivitySegments";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddPresenceActivitySegments_WhenSessionsExist_ShouldGiveEachOneCoveringSegmentWithoutRichFields()
    {
        await using var db = new ApolloDbContext(ApolloDbContext.CreateOptions(fixture.NewDatabaseConnectionString()));
        var migrator = await MigrateToBeforeAsync(db);
        var factorio = await SeedSessionAsync(db, "alice", PresenceActivityType.Playing, "Factorio", TestTime.Now, TestTime.Now.AddHours(2));
        var spotify = await SeedSessionAsync(db, "bob", PresenceActivityType.Listening, "Spotify", TestTime.Now.AddDays(-20), TestTime.Now.AddDays(-20).AddMinutes(30));

        await migrator.MigrateAsync(cancellationToken: Ct);

        var segments = await db.PresenceActivitySegments.AsNoTracking().OrderBy(s => s.OccurredAt).ToListAsync(Ct);
        segments.Select(s => (s.GuildId, s.UserId, s.ActivityType, s.ActivityName, s.OccurredAt, s.EndedAt)).ShouldBe([
            (spotify.GuildId, spotify.UserId, spotify.ActivityType, spotify.ActivityName, spotify.OccurredAt, spotify.EndedAt),
            (factorio.GuildId, factorio.UserId, factorio.ActivityType, factorio.ActivityName, factorio.OccurredAt, factorio.EndedAt),
        ]);
        segments.ShouldAllBe(s => s.Details == null && s.State == null && s.LargeText == null && s.SmallText == null
            && s.PartyExtId == null && s.PartySize == null && s.PartyMax == null && s.ApplicationExtId == null
            && s.ReportedStart == null && s.ReportedEnd == null);
        (await PresenceCoverage.FindUncoveredAsync(db, Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task AddPresenceActivitySegments_WhenNoSessionsExist_ShouldAddNoSegments()
    {
        await using var db = new ApolloDbContext(ApolloDbContext.CreateOptions(fixture.NewDatabaseConnectionString()));
        var migrator = await MigrateToBeforeAsync(db);

        await migrator.MigrateAsync(cancellationToken: Ct);

        (await db.PresenceActivitySegments.CountAsync(Ct)).ShouldBe(0);
    }

    private static async Task<IMigrator> MigrateToBeforeAsync(ApolloDbContext db)
    {
        var migrations = db.Database.GetMigrations().ToList();
        var index = migrations.FindIndex(m => m.EndsWith(Migration, StringComparison.Ordinal));
        index.ShouldBeGreaterThan(0, $"the migration {Migration} exists");
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[index - 1], cancellationToken: Ct);
        return migrator;
    }

    private static async Task<PresenceActivitySession> SeedSessionAsync(
        ApolloDbContext db,
        string user,
        PresenceActivityType type,
        string name,
        DateTimeOffset from,
        DateTimeOffset until)
    {
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var session = new PresenceActivitySession
        {
            GuildId = await ids.ResolveGuildAsync(TestPlatform.Guild, Ct),
            OccurredAt = from,
            UserId = await ids.ResolveUserAsync(TestPlatform.User(user), Ct),
            ActivityType = type,
            ActivityName = name,
            EndedAt = until,
        };
        db.PresenceActivitySessions.Add(session);
        await db.SaveChangesAsync(Ct);
        return session;
    }
}
