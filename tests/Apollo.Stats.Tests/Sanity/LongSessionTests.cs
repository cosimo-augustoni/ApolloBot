using Apollo.Domain.Voice;
using Apollo.Stats.Tests.Raw;
using Apollo.Stats.Sanity;
using Apollo.Testing.Data;

namespace Apollo.Stats.Tests.Sanity;

public sealed class LongSessionTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_WhenAVoiceSessionLasted25Hours_ShouldReportIt_AndNotOneOf23Hours()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        var longOne = seed.Voice(seed.Alice, "2026-01-11T10:00:00Z", "2026-01-12T11:00:00Z").Build();
        await RawStatsSeed.AddAsync(db, Ct, longOne, seed.Voice(seed.Bob, "2026-01-11T10:00:00Z", "2026-01-12T09:00:00Z").Build());

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.LongSessions.Count.ShouldBe(1);
        report.LongSessions.Examples.ShouldBe(
            [new SanitySession("voice_sessions", longOne.Id, seed.Alice, longOne.OccurredAt, longOne.EndedAt)]);
    }

    [Fact]
    public async Task GetAsync_WhenAPresenceSessionLasted25Hours_ShouldReportIt()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        var longOne = seed.Presence(seed.Alice, "2026-01-11T10:00:00Z", "2026-01-12T11:00:00Z").Build();
        await RawStatsSeed.AddAsync(db, Ct, longOne, seed.Presence(seed.Bob, "2026-01-11T10:00:00Z", "2026-01-11T12:00:00Z").Build());

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.LongSessions.Examples.ShouldBe(
            [new SanitySession("presence_activity_sessions", longOne.Id, seed.Alice, longOne.OccurredAt, longOne.EndedAt)]);
    }

    [Fact]
    public async Task GetAsync_ShouldReportLongSessionsThatStartedInTheRange_NewestFirst()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        var voice = seed.Voice(seed.Alice, "2026-01-10T10:00:00Z", "2026-01-11T11:00:00Z")
            .Until(SanitySeed.Utc("2026-01-11T11:00:00Z"), VoiceSessionEndReason.Unobserved).Build();
        var presence = seed.Presence(seed.Bob, "2026-01-12T10:00:00Z", "2026-01-13T11:00:00Z").Build();
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            voice,
            presence,
            seed.Voice(seed.Alice, "2026-01-08T10:00:00Z", "2026-01-09T11:00:00Z").Build(), // before the range
            seed.Voice(seed.Alice, "2026-01-15T10:00:00Z", "2026-01-16T11:00:00Z").Build()); // after it

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.LongSessions.Count.ShouldBe(2);
        report.LongSessions.Examples.Select(s => s.Id).ShouldBe([presence.Id, voice.Id]);
    }

    [Fact]
    public async Task GetAsync_WhenAVoiceSessionIsOpenSince30Hours_ShouldReportIt()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        // Now is 2026-01-15T09:00Z.
        var stale = seed.Voice(seed.Alice, "2026-01-14T03:00:00Z", null).Build();
        await RawStatsSeed.AddAsync(db, Ct, stale, seed.Voice(seed.Bob, "2026-01-14T10:00:00Z", null).Build());

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.OpenSessions.Count.ShouldBe(1);
        report.OpenSessions.Examples.ShouldBe([new SanitySession("voice_sessions", stale.Id, seed.Alice, stale.OccurredAt, null)]);
        report.LongSessions.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetAsync_ShouldReportAnOpenSessionThatStartedBeforeTheRange()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SanitySeed.CreateAsync(db, Ct);
        await RawStatsSeed.AddAsync(
            db,
            Ct,
            seed.Voice(seed.Alice, "2025-11-01T10:00:00Z", null).Build(),
            seed.Voice(seed.Bob, "2026-01-15T00:00:00Z", null).Build()); // after the range

        var report = await SanitySeed.Service(db).GetAsync(seed.Query(), Ct);

        report.OpenSessions.Examples.Select(s => s.UserId).ShouldBe([seed.Alice]);
    }
}
