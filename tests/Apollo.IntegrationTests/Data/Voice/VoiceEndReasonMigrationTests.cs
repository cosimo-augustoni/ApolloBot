using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Voice;
using Apollo.Domain.Voice;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests.Data.Voice;

/// <summary>
/// #98 renamed the end reason <c>BotRestart</c> to <c>Unobserved</c>; production already has rows with the old value.
/// </summary>
[Collection<TimescaleCollection>]
public sealed class VoiceEndReasonMigrationTests(TimescaleFixture fixture)
{
    private const string Migration = "_RenameVoiceEndReasonUnobserved";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RenameVoiceEndReasonUnobserved_WhenRowsSayBotRestart_ShouldRewriteThemToUnobserved()
    {
        await using var db = new ApolloDbContext(ApolloDbContext.CreateOptions(fixture.NewDatabaseConnectionString()));
        var migrations = db.Database.GetMigrations().ToList();
        var index = migrations.FindIndex(m => m.EndsWith(Migration, StringComparison.Ordinal));
        index.ShouldBeGreaterThan(0, $"the migration {Migration} exists");
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(migrations[index - 1], cancellationToken: Ct);
        var (restarted, left) = (await SeedSessionAsync(db, "alice"), await SeedSessionAsync(db, "bob"));
        await db.Database.ExecuteSqlAsync($"UPDATE voice_sessions SET end_reason = 'BotRestart' WHERE id = {restarted.Id}", Ct);
        await db.Database.ExecuteSqlAsync($"UPDATE voice_sessions SET end_reason = 'Left' WHERE id = {left.Id}", Ct);

        await migrator.MigrateAsync(cancellationToken: Ct);

        var reasons = await db.VoiceSessions.AsNoTracking().OrderBy(s => s.Id).Select(s => s.EndReason).ToListAsync(Ct);
        reasons.ShouldBe([VoiceSessionEndReason.Unobserved, VoiceSessionEndReason.Left]);
    }

    private static async Task<VoiceSession> SeedSessionAsync(ApolloDbContext db, string user)
    {
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var session = new VoiceSession
        {
            GuildId = await ids.ResolveGuildAsync(TestPlatform.Guild, Ct),
            OccurredAt = TestTime.Now,
            UserId = await ids.ResolveUserAsync(TestPlatform.User(user), Ct),
            ChannelId = await ids.ResolveChannelAsync(TestPlatform.Guild, TestPlatform.Channel("lounge"), Ct),
            EndedAt = TestTime.Now.AddHours(1),
        };
        db.VoiceSessions.Add(session);
        await db.SaveChangesAsync(Ct);
        return session;
    }
}
