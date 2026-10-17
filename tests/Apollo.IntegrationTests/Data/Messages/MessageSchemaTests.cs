using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;

namespace Apollo.IntegrationTests.Data.Messages;

public sealed class MessageSchemaTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    public static TheoryData<string> EventTables => ["message_events", "message_mentions"];

    [Theory]
    [MemberData(nameof(EventTables))]
    public async Task Table_WhenMigrated_ShouldBeAHypertablePartitionedByOccurredAt(string table)
    {
        await using var db = Fixture.CreateDbContext();

        var partitionColumns = await db.Database.SqlQuery<string>(
                $"""
                SELECT d.column_name AS "Value"
                FROM timescaledb_information.dimensions d
                WHERE d.hypertable_schema = 'public' AND d.hypertable_name = {table}
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        partitionColumns.ShouldBe(["occurred_at"]);
    }

    [Theory]
    [MemberData(nameof(EventTables))]
    public async Task Table_WhenMigrated_ShouldHaveAPrimaryKeyOnIdAndOccurredAt(string table)
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
                WHERE n.nspname = 'public' AND t.relname = {table} AND i.indisprimary
                GROUP BY i.indexrelid
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        primaryKey.ShouldBe(["id,occurred_at"]);
    }

    /// <summary>
    /// Message content is never stored (scope §5.1): the only text columns are the platform's message id and the kind.
    /// </summary>
    [Theory]
    [InlineData("message_events", new[] { "kind", "message_ext_id" })]
    [InlineData("message_mentions", new[] { "message_ext_id" })]
    public async Task Table_WhenMigrated_ShouldHaveNoTextColumnThatCouldHoldContent(string table, string[] expected)
    {
        await using var db = Fixture.CreateDbContext();

        var textColumns = await db.Database.SqlQuery<string>(
                $"""
                SELECT column_name AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = {table}
                  AND data_type IN ('text', 'character varying', 'character', 'json', 'jsonb', 'bytea')
                ORDER BY column_name
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        textColumns.ShouldBe(expected);
    }
}
