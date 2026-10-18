using System.Globalization;
using Apollo.Data.Identity;
using Apollo.Data.Membership;
using Apollo.Data.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Stats.Sanity;
using Apollo.Stats.Tests.Raw;
using Apollo.Testing.Data;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.Stats.Tests.Sanity;

public sealed class UntrackedEventTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_WhenEveryEventIsInsideATrackingPeriod_ShouldReportZero()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, seed.EveryoneTracked());
        await RawStatsSeed.AddAsync(db, Ct, [.. OneRowPerTable(seed, seed.Alice, "2026-01-11T10:00:00Z")]);

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.UntrackedEvents.Count.ShouldBe(0);
        report.UntrackedEvents.Examples.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetAsync_WhenAMessageEventHasNoCoveringTrackingPeriod_ShouldReportItAsTheExample()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, seed.Period(seed.Alice, "2026-01-12T00:00:00Z", null));
        var message = seed.Message(seed.Alice, "2026-01-11T10:00:00Z").Build();
        await RawStatsSeed.AddAsync(db, Ct, message, seed.Message(seed.Alice, "2026-01-12T10:00:00Z").Build());

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.UntrackedEvents.Count.ShouldBe(1);
        report.UntrackedEvents.Examples.ShouldBe(
            [new UntrackedRow("message_events", "user_id", message.Id, seed.Alice, message.OccurredAt)]);
    }

    [Fact]
    public async Task GetAsync_ShouldCheckEveryEventTable()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, [.. OneRowPerTable(seed, seed.Bob, "2026-01-11T10:00:00Z")]);

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.UntrackedEvents.Count.ShouldBe(7);
        report.UntrackedEvents.Examples.Select(r => $"{r.Table}.{r.Column}").Order().ShouldBe(
        [
            "membership_events.user_id",
            "message_events.user_id",
            "message_mentions.user_id",
            "presence_activity_sessions.user_id",
            "reaction_events.user_id",
            "voice_sessions.user_id",
            "voice_state_segments.user_id",
        ]);
        report.UntrackedEvents.Examples.ShouldAllBe(r => r.UserId == seed.Bob);
    }

    [Fact]
    public async Task GetAsync_ShouldCoverAPeriodFromItsStartUpToButExcludingItsEnd()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, seed.Period(seed.Alice, "2026-01-11T10:00:00Z", "2026-01-12T10:00:00Z"));
        var atEnd = seed.Message(seed.Alice, "2026-01-12T10:00:00Z").Build();
        await RawStatsSeed.AddAsync(db, Ct, seed.Message(seed.Alice, "2026-01-11T10:00:00Z").Build(), atEnd);

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.UntrackedEvents.Examples.Select(r => r.Id).ShouldBe([atEnd.Id]);
    }

    [Fact]
    public async Task GetAsync_ShouldOnlyCountPeriodsOfTheSameUserAndGuild()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var otherGuild = await new GuildBuilder().For(new PlatformRef(PlatformKind.Discord, "other-guild")).SeedAsync(db, ids, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Period(seed.Bob, "2026-01-01T00:00:00Z", null),
            new TrackingPeriod { GuildId = otherGuild.Id, UserId = seed.Alice, From = SanitySeed.Utc("2026-01-01T00:00:00Z") });
        await RawStatsSeed.AddAsync(db, Ct, seed.Message(seed.Alice, "2026-01-11T10:00:00Z").Build());

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.UntrackedEvents.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetAsync_ShouldOnlyReadEventsInTheRange()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Message(seed.Alice, "2026-01-09T22:59:59Z").Build(),
            seed.Message(seed.Alice, "2026-01-14T23:00:00Z").Build());

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.UntrackedEvents.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetAsync_WhenALeaveEndsTheTrackingPeriod_ShouldCountItAsTracked()
    {
        // The membership recorder keeps a leave at the instant tracking stopped, and a ban up to 10 s after that leave (#18).
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, seed.Period(seed.Alice, "2026-01-01T00:00:00Z", "2026-01-11T10:00:00Z"));
        var lateBan = seed.Membership(seed.Alice, MembershipEventKind.Ban, "2026-01-11T10:00:20Z");
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Membership(seed.Alice, MembershipEventKind.Leave, "2026-01-11T10:00:00Z"),
            seed.Membership(seed.Alice, MembershipEventKind.Ban, "2026-01-11T10:00:05Z"),
            lateBan);

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.UntrackedEvents.Examples.Select(r => r.Id).ShouldBe([lateBan.Id]);
    }

    [Fact]
    public async Task GetAsync_WhenAJoinIsAtTheEndOfAPeriod_ShouldReportIt()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(db, Ct, seed.Period(seed.Alice, "2026-01-01T00:00:00Z", "2026-01-11T10:00:00Z"));
        await RawStatsSeed.AddAsync(db, Ct, seed.Membership(seed.Alice, MembershipEventKind.Join, "2026-01-11T10:00:00Z"));

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.UntrackedEvents.Count.ShouldBe(1);
    }

    private static IEnumerable<object> OneRowPerTable(SanitySeed seed, Guid userId, string atUtc)
    {
        var anHourLater = SanitySeed.Utc(atUtc).AddHours(1).ToString("O", CultureInfo.InvariantCulture);
        yield return seed.Message(userId, atUtc).Build();
        yield return seed.Mention(userId, mentionedUserId: null, atUtc);
        yield return seed.Reaction(userId, atUtc).Build();
        yield return seed.Voice(userId, atUtc, anHourLater).Build();
        yield return seed.Segment(userId, atUtc);
        yield return seed.Membership(userId, MembershipEventKind.Join, atUtc);
        yield return seed.Presence(userId, atUtc, anHourLater).Build();
    }
}
