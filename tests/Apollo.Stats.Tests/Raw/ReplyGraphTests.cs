using Apollo.Data.Messages;
using Apollo.Stats.Raw;
using Apollo.Testing.Data;

namespace Apollo.Stats.Tests.Raw;

public sealed class ReplyGraphTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetReplyGraphAsync_ShouldCountTheRepliesPerDirectedEdge()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            Reply(seed, seed.Alice, seed.Bob, "2026-01-11T10:00:00Z"),
            Reply(seed, seed.Alice, seed.Bob, "2026-01-12T10:00:00Z"),
            Reply(seed, seed.Alice, seed.Bob, "2026-01-13T10:00:00Z"),
            Reply(seed, seed.Bob, seed.Alice, "2026-01-13T11:00:00Z"),
            Reply(seed, seed.Bob, seed.Alice, "2026-01-15T11:00:00Z")); // 15 Jan 12:00 CET, after the range

        var graph = await new RawStatsService(db).GetReplyGraphAsync(seed.Query(), Ct);

        graph.Edges.ShouldBe([new ReplyEdge(seed.Alice, seed.Bob, 3), new ReplyEdge(seed.Bob, seed.Alice, 1)]);
        (graph.RepliesTo, graph.RepliedBy).ShouldBe(([], []));
    }

    [Fact]
    public async Task GetReplyGraphAsync_ShouldLeaveOutMessagesWithoutRepliedToUserAndBotMessages()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            Reply(seed, seed.Alice, seed.Bob, "2026-01-11T10:00:00Z"),
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At("2026-01-11T10:05:00Z").Build(),
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Carol).ReplyTo(seed.Bob).FromBot().At("2026-01-11T10:10:00Z").Build(),
            Reply(seed, seed.Bot, seed.Bob, "2026-01-11T10:15:00Z"), // a bot member
            Reply(seed, seed.Bob, seed.Bot, "2026-01-11T10:20:00Z"), // a reply to a bot member
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).ReplyTo(seed.Bob).OfKind(MessageEventKind.Edit).At("2026-01-11T10:25:00Z").Build());

        var graph = await new RawStatsService(db).GetReplyGraphAsync(seed.Query(), Ct);

        graph.Edges.ShouldBe([new ReplyEdge(seed.Alice, seed.Bob, 1)]);
    }

    [Fact]
    public async Task GetReplyGraphAsync_ShouldLeaveOutRepliesToOneself()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, Reply(seed, seed.Alice, seed.Alice, "2026-01-11T10:00:00Z"));

        var graph = await new RawStatsService(db).GetReplyGraphAsync(seed.Query(), Ct);

        graph.Edges.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetReplyGraphAsync_ShouldReturnTheTopEdges()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            Reply(seed, seed.Alice, seed.Bob, "2026-01-11T10:00:00Z"),
            Reply(seed, seed.Carol, seed.Bob, "2026-01-11T10:00:00Z"),
            Reply(seed, seed.Carol, seed.Bob, "2026-01-11T11:00:00Z"),
            Reply(seed, seed.Bob, seed.Carol, "2026-01-11T11:00:00Z"));

        var graph = await new RawStatsService(db).GetReplyGraphAsync(seed.Query(top: 1), Ct);

        graph.Edges.ShouldBe([new ReplyEdge(seed.Carol, seed.Bob, 2)]);
    }

    [Fact]
    public async Task GetReplyGraphAsync_ForAUser_ShouldReturnOnlyTheEdgesInvolvingThem()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await RawStatsSeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            Reply(seed, seed.Alice, seed.Bob, "2026-01-11T10:00:00Z"),
            Reply(seed, seed.Alice, seed.Bob, "2026-01-11T11:00:00Z"),
            Reply(seed, seed.Alice, seed.Carol, "2026-01-11T11:00:00Z"),
            Reply(seed, seed.Carol, seed.Alice, "2026-01-11T12:00:00Z"),
            Reply(seed, seed.Bob, seed.Carol, "2026-01-11T12:00:00Z"));

        var graph = await new RawStatsService(db).GetReplyGraphAsync(seed.Query(seed.Alice), Ct);

        graph.RepliesTo.ShouldBe([new ReplyEdge(seed.Alice, seed.Bob, 2), new ReplyEdge(seed.Alice, seed.Carol, 1)]);
        graph.RepliedBy.ShouldBe([new ReplyEdge(seed.Carol, seed.Alice, 1)]);
        graph.Edges.ShouldNotContain(e => e.AuthorId != seed.Alice && e.RepliedToId != seed.Alice);
        graph.Edges.Count.ShouldBe(3);
    }

    private static MessageEvent Reply(RawStatsSeed seed, Guid authorId, Guid repliedToId, string atUtc) =>
        new MessageEventBuilder().In(seed.GuildId, seed.General).By(authorId).ReplyTo(repliedToId).At(atUtc).Build();
}
