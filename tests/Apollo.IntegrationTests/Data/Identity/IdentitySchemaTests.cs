using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;

namespace Apollo.IntegrationTests.Data.Identity;

public sealed class IdentitySchemaTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    [Theory]
    [InlineData("guild_platform_ids")]
    [InlineData("user_platform_ids")]
    [InlineData("channel_platform_ids")]
    [InlineData("role_platform_ids")]
    public async Task MappingTable_WhenMigrated_ShouldHaveUniqueIndexOnPlatformAndExternalId(string table)
    {
        await using var db = Fixture.CreateDbContext();

        var uniqueIndexColumns = await db.Database.SqlQuery<string>(
                $"""
                SELECT string_agg(a.attname, ',' ORDER BY k.ord) AS "Value"
                FROM pg_index i
                JOIN pg_class t ON t.oid = i.indrelid
                JOIN pg_namespace n ON n.oid = t.relnamespace
                CROSS JOIN LATERAL unnest(i.indkey) WITH ORDINALITY AS k(attnum, ord)
                JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = k.attnum
                WHERE n.nspname = 'public' AND t.relname = {table} AND i.indisunique
                GROUP BY i.indexrelid
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        uniqueIndexColumns.ShouldContain("platform,external_id");
    }
}
