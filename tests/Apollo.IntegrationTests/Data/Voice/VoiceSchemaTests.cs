using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;

namespace Apollo.IntegrationTests.Data.Voice;

public sealed class VoiceSchemaTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    public static TheoryData<string> VoiceTables => ["voice_sessions", "voice_state_segments"];

    [Theory]
    [MemberData(nameof(VoiceTables))]
    public async Task Table_WhenMigrated_ShouldBeAHypertablePartitionedByOccurredAt(string table)
    {
        await using var db = Fixture.CreateDbContext();

        var partitionColumns = await db.Database.SqlQuery<string>(
                $"""
                SELECT d.column_name AS "Value"
                FROM timescaledb_information.hypertables h
                JOIN timescaledb_information.dimensions d
                  ON d.hypertable_schema = h.hypertable_schema AND d.hypertable_name = h.hypertable_name
                WHERE h.hypertable_schema = 'public' AND h.hypertable_name = {table}
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        partitionColumns.ShouldBe(["occurred_at"]);
    }

    [Theory]
    [MemberData(nameof(VoiceTables))]
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
    /// The startup close finds the rows a previous run left open through a partial index, not a scan of every chunk.
    /// </summary>
    [Theory]
    [MemberData(nameof(VoiceTables))]
    public async Task Table_WhenMigrated_ShouldHaveAPartialIndexOnOpenRows(string table)
    {
        await using var db = Fixture.CreateDbContext();

        var definitions = await db.Database.SqlQuery<string>(
                $"""
                SELECT indexdef AS "Value"
                FROM pg_indexes
                WHERE schemaname = 'public' AND tablename = {table}
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        definitions.ShouldContain(d => d.EndsWith("WHERE (ended_at IS NULL)", StringComparison.Ordinal));
    }
}
