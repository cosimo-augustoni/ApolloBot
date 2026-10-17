using Apollo.Data.Identity;
using Apollo.Data.Reactions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests.Data.Reactions;

public sealed class ReactionSchemaTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    [Fact]
    public async Task ReactionEvents_WhenMigrated_ShouldBeAHypertablePartitionedByOccurredAt()
    {
        await using var db = Fixture.CreateDbContext();

        var partitionColumns = await db.Database.SqlQuery<string>(
                $"""
                SELECT d.column_name AS "Value"
                FROM timescaledb_information.hypertables h
                JOIN timescaledb_information.dimensions d
                  ON d.hypertable_schema = h.hypertable_schema AND d.hypertable_name = h.hypertable_name
                WHERE h.hypertable_schema = 'public' AND h.hypertable_name = 'reaction_events'
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        partitionColumns.ShouldBe(["occurred_at"]);
    }

    [Fact]
    public async Task ReactionEvents_WhenMigrated_ShouldHaveAPrimaryKeyOnIdAndOccurredAt()
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
                WHERE n.nspname = 'public' AND t.relname = 'reaction_events' AND i.indisprimary
                GROUP BY i.indexrelid
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        primaryKey.ShouldBe(["id,occurred_at"]);
    }

    /// <summary>
    /// Nothing about the message itself is stored (scope §5.1): the only text columns are its platform id, the emoji
    /// and the kind.
    /// </summary>
    [Fact]
    public async Task ReactionEvents_WhenMigrated_ShouldHaveNoTextColumnThatCouldHoldContent()
    {
        await using var db = Fixture.CreateDbContext();

        var textColumns = await db.Database.SqlQuery<string>(
                $"""
                SELECT column_name AS "Value"
                FROM information_schema.columns
                WHERE table_schema = 'public' AND table_name = 'reaction_events'
                  AND data_type IN ('text', 'character varying', 'character', 'json', 'jsonb', 'bytea')
                ORDER BY column_name
                """)
            .ToListAsync(TestContext.Current.CancellationToken);

        textColumns.ShouldBe(["emoji_custom_id", "emoji_name", "kind", "message_ext_id"]);
    }

    /// <summary>
    /// A row without a custom emoji id is a unicode emoji, so its name is the emoji itself and cannot be empty.
    /// </summary>
    [Fact]
    public async Task ReactionEvents_WhenAUnicodeRowHasNoEmoji_ShouldRejectIt()
    {
        await using var db = Fixture.CreateDbContext();
        var ct = TestContext.Current.CancellationToken;
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var channel = await new ChannelBuilder().SeedAsync(db, ids, ct);
        var user = await new UserBuilder().SeedAsync(db, ids, ct);
        db.ReactionEvents.Add(new ReactionEvent
        {
            GuildId = channel.GuildId,
            OccurredAt = TestTime.Now,
            UserId = user.Id,
            ChannelId = channel.Id,
            MessageExtId = "m1",
            EmojiName = string.Empty,
            EmojiCustomId = null,
            Kind = ReactionEventKind.Add,
        });

        await Should.ThrowAsync<DbUpdateException>(() => db.SaveChangesAsync(ct));
    }
}
