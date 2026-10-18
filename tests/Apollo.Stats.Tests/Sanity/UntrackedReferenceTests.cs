using Apollo.Stats.Tests.Raw;
using Apollo.Stats.Sanity;
using Apollo.Testing.Data;

namespace Apollo.Stats.Tests.Sanity;

public sealed class UntrackedReferenceTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_WhenAReplyReferencesAUserWhoWasNotTrackedThen_ShouldReportIt()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Period(seed.Alice, "2026-01-01T00:00:00Z", null),
            seed.Period(seed.Bob, "2026-01-12T00:00:00Z", null));
        var reply = seed.Message(seed.Alice, "2026-01-11T10:00:00Z").ReplyTo(seed.Bob).Build();
        await RawStatsSeed.AddAsync(db, Ct, reply, seed.Message(seed.Alice, "2026-01-12T10:00:00Z").ReplyTo(seed.Bob).Build());

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.UntrackedReferences.Count.ShouldBe(1);
        report.UntrackedReferences.Examples.ShouldBe(
            [new UntrackedRow("message_events", "reply_to_user_id", reply.Id, seed.Bob, reply.OccurredAt)]);
        report.UntrackedEvents.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetAsync_ShouldCheckMentionsAndReactionAuthors()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, seed.Period(seed.Alice, "2026-01-01T00:00:00Z", null));
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Mention(seed.Alice, seed.Bob, "2026-01-11T10:00:00Z"),
            seed.Reaction(seed.Alice, "2026-01-11T11:00:00Z").OnMessageBy(seed.Bob).Build());

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.UntrackedReferences.Examples.Select(r => $"{r.Table}.{r.Column}").ShouldBe(
            ["reaction_events.message_author_id", "message_mentions.mentioned_user_id"]);
        report.UntrackedReferences.Examples.ShouldAllBe(r => r.UserId == seed.Bob);
    }

    [Fact]
    public async Task GetAsync_WhenReferencesAreTrackedOrNull_ShouldReportZero()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, seed.EveryoneTracked());
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Message(seed.Alice, "2026-01-11T10:00:00Z").ReplyTo(seed.Bob).Build(),
            seed.Message(seed.Alice, "2026-01-11T10:00:00Z").Build(),
            seed.Mention(seed.Alice, seed.Bob, "2026-01-11T10:00:00Z"),
            seed.Mention(seed.Alice, null, "2026-01-11T10:00:00Z"),
            seed.Reaction(seed.Alice, "2026-01-11T11:00:00Z").OnMessageBy(seed.Bob).Build(),
            seed.Reaction(seed.Alice, "2026-01-11T11:00:00Z").OnMessageBy(null).Build());

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.UntrackedReferences.Count.ShouldBe(0);
    }
}
