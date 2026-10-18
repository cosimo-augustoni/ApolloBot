using Apollo.Stats.Raw;
using Apollo.Testing.Data;

namespace Apollo.Stats.Tests.Raw;

public sealed class TopEmojisTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetTopEmojisAsync_ShouldCountAddedReactionsOnly()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            Reaction(seed, seed.Alice).WithEmoji("👍").Build(),
            Reaction(seed, seed.Bob).WithEmoji("👍").Build(),
            Reaction(seed, seed.Bob).WithEmoji("👍").Removed().Build(),
            Reaction(seed, seed.Bob).WithEmoji("🎉").Removed().Build(),
            Reaction(seed, seed.Bob).WithEmoji("👍").At("2026-01-15T00:00:00Z").Build()); // after the range

        var emojis = await new RawStatsService(db).GetTopEmojisAsync(seed.Query(), Ct);

        emojis.ShouldBe([new EmojiUsage("👍", null, 2)]);
    }

    [Fact]
    public async Task GetTopEmojisAsync_ShouldTellCustomAndUnicodeEmojisApart()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            Reaction(seed, seed.Alice).WithEmoji("🔥").Build(),
            Reaction(seed, seed.Alice).WithCustomEmoji("111", "🔥").Build(), // a custom emoji named like a unicode one
            Reaction(seed, seed.Bob).WithCustomEmoji("111", "🔥").Build(),
            Reaction(seed, seed.Bob).WithCustomEmoji("222", "pepe").At("2026-01-11T10:00:00Z").Build(),
            Reaction(seed, seed.Bob).WithCustomEmoji("222", "pepe").At("2026-01-11T10:00:00Z").Build(),
            Reaction(seed, seed.Carol).WithCustomEmoji("222", "pepe_happy").At("2026-01-12T10:00:00Z").Build()); // renamed

        var emojis = await new RawStatsService(db).GetTopEmojisAsync(seed.Query(), Ct);

        emojis.ShouldBe([new EmojiUsage("pepe_happy", "222", 3), new EmojiUsage("🔥", "111", 2), new EmojiUsage("🔥", null, 1)]);
    }

    [Fact]
    public async Task GetTopEmojisAsync_ShouldOrderTiesDeterministicallyAndReturnTheTopN()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            Reaction(seed, seed.Alice).WithEmoji("😀").Build(),
            Reaction(seed, seed.Alice).WithCustomEmoji("2", "b").Build(),
            Reaction(seed, seed.Alice).WithCustomEmoji("1", "b").Build(),
            Reaction(seed, seed.Alice).WithCustomEmoji("3", "a").Build());

        var emojis = await new RawStatsService(db).GetTopEmojisAsync(seed.Query(top: 3), Ct);

        // Same count: by name (ordinal), then unicode before custom, then by custom ID.
        emojis.ShouldBe([new EmojiUsage("a", "3", 1), new EmojiUsage("b", "1", 1), new EmojiUsage("b", "2", 1)]);
    }

    [Fact]
    public async Task GetTopEmojisAsync_WhenNameAndCountAreEqual_ShouldPutUnicodeBeforeCustom()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            Reaction(seed, seed.Alice).WithCustomEmoji("111", "🔥").Build(),
            Reaction(seed, seed.Alice).WithEmoji("🔥").Build());

        var emojis = await new RawStatsService(db).GetTopEmojisAsync(seed.Query(), Ct);

        emojis.ShouldBe([new EmojiUsage("🔥", null, 1), new EmojiUsage("🔥", "111", 1)]);
    }

    [Fact]
    public async Task GetTopEmojisAsync_ShouldLeaveOutReactionsByBotsUnlessIncluded()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            Reaction(seed, seed.Alice).WithEmoji("👍").Build(),
            Reaction(seed, seed.Bot).WithEmoji("🤖").Build());

        var service = new RawStatsService(db);
        var withoutBots = await service.GetTopEmojisAsync(seed.Query(), Ct);
        var withBots = await service.GetTopEmojisAsync(seed.Query(includeBots: true), Ct);

        withoutBots.ShouldBe([new EmojiUsage("👍", null, 1)]);
        withBots.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetTopEmojisAsync_ForAUser_ShouldCountOnlyTheirReactions()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            Reaction(seed, seed.Alice).WithEmoji("👍").Build(),
            Reaction(seed, seed.Bob).WithEmoji("🎉").OnMessageBy(seed.Alice).Build());

        var emojis = await new RawStatsService(db).GetTopEmojisAsync(seed.Query(seed.Alice), Ct);

        emojis.ShouldBe([new EmojiUsage("👍", null, 1)]);
    }

    private static ReactionEventBuilder Reaction(RawStatsSeed seed, Guid userId) =>
        new ReactionEventBuilder().In(seed.GuildId, seed.General).By(userId).At("2026-01-11T10:00:00Z");
}
