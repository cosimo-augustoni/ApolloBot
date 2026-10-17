using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;

namespace Apollo.IntegrationTests.Data.Membership;

public sealed class MembershipSchemaTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    [Fact]
    public async Task MembershipEvents_WhenMigrated_ShouldBeAHypertablePartitionedByOccurredAt()
    {
        await using var db = Fixture.CreateDbContext();

        var partitionColumns = await db.Database.SqlQuery<string>(
                $"""
                SELECT d.column_name AS "Value"
                FROM timescaledb_information.hypertables h
                JOIN timescaledb_information.dimensions d
                  ON d.hypertable_schema = h.hypertable_schema AND d.hypertable_name = h.hypertable_name
                WHERE h.hypertable_schema = 'public' AND h.hypertable_name = 'membership_events'
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        partitionColumns.ShouldBe(["occurred_at"]);
    }

    [Fact]
    public async Task MembershipEvents_WhenMigrated_ShouldHaveAPrimaryKeyOnIdAndOccurredAt()
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
                WHERE n.nspname = 'public' AND t.relname = 'membership_events' AND i.indisprimary
                GROUP BY i.indexrelid
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        primaryKey.ShouldBe(["id,occurred_at"]);
    }
}
