using Apollo.Data;
using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Apollo.IntegrationTests.Data.Activity;

/// <summary>
/// <c>AddActivityAggregates</c> (#34) is partly hand-written and not atomic (continuous aggregates are created outside
/// a transaction), so its Down must remove exactly what its Up created.
/// </summary>
[Collection<TimescaleCollection>]
public sealed class ActivityAggregatesMigrationTests(TimescaleFixture fixture)
{
    private const string Migration = "_AddActivityAggregates";

    private static readonly string[] _relations =
    [
        "daily_guild_activity",
        "daily_user_channel_activity",
        "daily_user_channel_messages",
        "daily_user_channel_reactions_given",
        "daily_user_channel_reactions_received",
        "daily_user_channel_voice",
        "hourly_activity_heatmap",
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AddActivityAggregates_WhenRolledBackAndReapplied_ShouldDropAndRecreateEveryAggregate()
    {
        await using var db = new ApolloDbContext(ApolloDbContext.CreateOptions(fixture.NewDatabaseConnectionString()));
        var migrations = db.Database.GetMigrations().ToList();
        var index = migrations.FindIndex(m => m.EndsWith(Migration, StringComparison.Ordinal));
        index.ShouldBeGreaterThan(0, $"the migration {Migration} exists");
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(cancellationToken: Ct);

        await migrator.MigrateAsync(migrations[index - 1], cancellationToken: Ct);
        var afterDown = await ActivityRelationsAsync(db);
        await migrator.MigrateAsync(cancellationToken: Ct);
        var afterUp = await ActivityRelationsAsync(db);

        afterDown.ShouldBeEmpty();
        afterUp.ShouldBe(_relations);
    }

    private static async Task<List<string>> ActivityRelationsAsync(ApolloDbContext db) =>
        await db.Database.SqlQuery<string>(
                $"""
                SELECT c.relname AS "Value"
                FROM pg_class c
                JOIN pg_namespace n ON n.oid = c.relnamespace
                WHERE n.nspname = 'public' AND c.relkind = 'v' AND c.relname = ANY ({_relations})
                ORDER BY c.relname
                """)
            .ToListAsync(Ct);
}
