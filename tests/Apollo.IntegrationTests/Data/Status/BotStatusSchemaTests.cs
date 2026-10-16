using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;

namespace Apollo.IntegrationTests.Data.Status;

public sealed class BotStatusSchemaTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    [Fact]
    public async Task BotStatus_WhenMigrated_ShouldBeAHypertablePartitionedByOccurredAt()
    {
        await using var db = Fixture.CreateDbContext();

        var partitionColumns = await db.Database.SqlQuery<string>(
                $"""
                SELECT d.column_name AS "Value"
                FROM timescaledb_information.dimensions d
                WHERE d.hypertable_schema = 'public' AND d.hypertable_name = 'bot_status'
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        partitionColumns.ShouldBe(["occurred_at"]);
    }

    [Fact]
    public async Task BotStatus_WhenMigrated_ShouldHaveAPrimaryKeyOnIdAndOccurredAt()
    {
        await using var db = Fixture.CreateDbContext();

        var primaryKey = await db.Database.SqlQuery<string>(
                $"""
                SELECT string_agg(a.attname, ',' ORDER BY k.ord) AS "Value"
                FROM pg_index i
                JOIN pg_class t ON t.oid = i.indrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ord)
                JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
                WHERE n.nspname = 'public' AND t.relname = 'bot_status' AND i.indisprimary
                GROUP BY i.indexrelid
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        primaryKey.ShouldBe(["id,occurred_at"]);
    }
}
