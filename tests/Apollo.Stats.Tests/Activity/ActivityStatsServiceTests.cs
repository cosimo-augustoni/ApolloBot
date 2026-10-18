using System.Globalization;
using System.Text.Json;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Messages;
using Apollo.Data.Tracking;
using Apollo.Stats.Activity;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.Stats.Tests.Activity;

/// <summary>
/// <see cref="ActivityStatsService"/> over the activity aggregates (#34). Every test seeds its own guild (a new internal
/// ID after the reset), so materialized rows of earlier tests never show up.
/// </summary>
public sealed class ActivityStatsServiceTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly DateOnly _jan10 = new(2026, 1, 10);
    private static readonly DateOnly _jan14 = new(2026, 1, 14);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetAsync_ForTheGuild_ShouldSumTheActivityOfAllUsers()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Message(seed, seed.Alice, "2026-01-12T10:00:00Z", length: 10),
            Message(seed, seed.Alice, "2026-01-13T10:00:00Z", length: 20),
            Message(seed, seed.Bob, "2026-01-13T11:00:00Z", length: 5),
            new ReactionEventBuilder().In(seed.GuildId, seed.General).By(seed.Bob).OnMessageBy(seed.Alice).At("2026-01-13T12:00:00Z").Build(),
            new VoiceSessionBuilder().In(seed.GuildId, seed.Lounge).By(seed.Bob).From("2026-01-12T18:00:00Z").Until("2026-01-12T19:00:00Z").Build());

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, _jan10, _jan14));

        stats.Totals.ShouldBe(new ActivityCounts(Messages: 3, Characters: 35, ReactionsGiven: 1, ReactionsReceived: 1, VoiceSeconds: 3600));
    }

    [Fact]
    public async Task GetAsync_ForTheGuild_ShouldCountActiveDaysAndDistinctActiveUsers()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Message(seed, seed.Alice, "2026-01-12T10:00:00Z"),
            Message(seed, seed.Alice, "2026-01-13T10:00:00Z"),
            new VoiceSessionBuilder().In(seed.GuildId, seed.Lounge).By(seed.Bob).From("2026-01-13T18:00:00Z").Until("2026-01-13T19:00:00Z").Build());

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, _jan10, _jan14));

        (stats.ActiveDays, stats.ActiveUsers, stats.TrackedDays, stats.TrackedSince).ShouldBe((2, (int?)2, (int?)null, (DateOnly?)null));
    }

    [Fact]
    public async Task GetAsync_ForTheGuild_ShouldAverageOverEveryDayOfTheRange()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Message(seed, seed.Alice, "2026-01-12T10:00:00Z", length: 30),
            Message(seed, seed.Bob, "2026-01-13T10:00:00Z", length: 20));

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, _jan10, _jan14));

        stats.Averages.ShouldBe(new ActivityAverages(Messages: 0.4, Characters: 10, ReactionsGiven: 0, ReactionsReceived: 0, VoiceSeconds: 0));
    }

    [Fact]
    public async Task GetAsync_ForAUser_ShouldReturnOnlyTheirActivity()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Message(seed, seed.Alice, "2026-01-12T10:00:00Z", length: 10),
            Message(seed, seed.Bob, "2026-01-12T11:00:00Z", length: 99),
            Message(seed, seed.Bob, "2026-01-13T11:00:00Z", length: 99, channelId: seed.Lounge),
            new ReactionEventBuilder().In(seed.GuildId, seed.General).By(seed.Bob).OnMessageBy(seed.Alice).At("2026-01-12T12:00:00Z").Build(),
            new VoiceSessionBuilder().In(seed.GuildId, seed.Lounge).By(seed.Bob).From("2026-01-12T18:00:00Z").Until("2026-01-12T19:00:00Z").Build());

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, seed.Alice, _jan10, _jan14));

        stats.Totals.ShouldBe(new ActivityCounts(Messages: 1, Characters: 10, ReactionsGiven: 0, ReactionsReceived: 1, VoiceSeconds: 0));
        stats.ActiveDays.ShouldBe(1);
        stats.ActiveUsers.ShouldBeNull();
        stats.Channels.Select(c => (c.ChannelId, c.Messages)).ShouldBe([(seed.General, 1L)]);
        stats.Heatmap.Sum(day => day.Sum()).ShouldBe(1);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 3)]
    public async Task GetAsync_WithBotMessages_ShouldCountThemOnlyWhenBotsAreIncluded(bool includeBots, long expectedMessages)
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Message(seed, seed.Alice, "2026-01-12T10:00:00Z"),
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Bot).At("2026-01-12T10:05:00Z").FromBot().Build(),
            // The member row says bot although the message row does not: tiles and heatmap both treat it as a bot (#34).
            Message(seed, seed.Bot, "2026-01-12T10:10:00Z"));

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, _jan10, _jan14, IncludeBots: includeBots));

        stats.Totals.Messages.ShouldBe(expectedMessages);
        stats.Daily.Sum(d => d.Counts.Messages).ShouldBe(expectedMessages);
        stats.Channels.Sum(c => c.Messages).ShouldBe(expectedMessages);
        stats.Heatmap.Sum(day => day.Sum()).ShouldBe(expectedMessages);
        stats.ActiveUsers.ShouldBe(includeBots ? 2 : 1);
    }

    [Fact]
    public async Task GetAsync_WithMessagesInAThread_ShouldRankThemUnderTheParentChannel()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Message(seed, seed.Alice, "2026-01-12T10:00:00Z", channelId: seed.Lounge),
            new MessageEventBuilder().In(seed.GuildId, seed.Thread, threadParentId: seed.General).By(seed.Alice).At("2026-01-12T10:00:00Z").Build(),
            new MessageEventBuilder().In(seed.GuildId, seed.Thread, threadParentId: seed.General).By(seed.Bob).At("2026-01-12T11:00:00Z").Build(),
            new VoiceSessionBuilder().In(seed.GuildId, seed.Lounge).By(seed.Bob).From("2026-01-12T18:00:00Z").Until("2026-01-12T18:01:00Z").Build());

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, _jan10, _jan14));

        stats.Channels.ShouldBe(
        [
            new ChannelActivity(seed.General, "general", IsDeleted: false, Messages: 2, VoiceSeconds: 0),
            new ChannelActivity(seed.Lounge, "lounge", IsDeleted: false, Messages: 1, VoiceSeconds: 60),
        ]);
    }

    [Fact]
    public async Task GetAsync_WithADeletedChannel_ShouldStillRankItFlaggedAsDeleted()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var gone = await new ChannelBuilder().For(TestPlatform.Channel("gone")).Named("old-news").DeletedAt(Utc("2026-01-13T00:00:00Z")).SeedAsync(db, ids, Ct);
        await AddAsync(db, Message(seed, seed.Alice, "2026-01-12T10:00:00Z", channelId: gone.Id));

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, _jan10, _jan14));

        stats.Channels.ShouldBe([new ChannelActivity(gone.Id, "old-news", IsDeleted: true, Messages: 1, VoiceSeconds: 0)]);
    }

    [Fact]
    public async Task GetAsync_ForARangeOfNDays_ShouldReturnOneZeroFilledEntryPerDay()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(db, Message(seed, seed.Alice, "2026-01-12T10:00:00Z", length: 7));

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, _jan10, _jan14));

        stats.Daily.ShouldBe(
        [
            new DailyActivity(new DateOnly(2026, 1, 10), ActivityCounts.Zero),
            new DailyActivity(new DateOnly(2026, 1, 11), ActivityCounts.Zero),
            new DailyActivity(new DateOnly(2026, 1, 12), new ActivityCounts(1, 7, 0, 0, 0)),
            new DailyActivity(new DateOnly(2026, 1, 13), ActivityCounts.Zero),
            new DailyActivity(_jan14, ActivityCounts.Zero),
        ]);
    }

    [Theory]
    [InlineData("2026-01-10", "2026-01-14", "2026-01-09T22:59:00Z", 0)] // 9 Jan 23:59 CET, the day before From
    [InlineData("2026-01-10", "2026-01-14", "2026-01-09T23:00:00Z", 1)] // 10 Jan 00:00 CET
    [InlineData("2026-01-10", "2026-01-14", "2026-01-14T22:30:00Z", 1)] // 14 Jan 23:30 CET
    [InlineData("2026-01-10", "2026-01-14", "2026-01-14T23:30:00Z", 0)] // 15 Jan 00:30 CET, the day after To
    [InlineData("2026-07-14", "2026-07-14", "2026-07-14T21:30:00Z", 1)] // 14 Jul 23:30 CEST
    [InlineData("2026-07-14", "2026-07-14", "2026-07-14T22:30:00Z", 0)] // 15 Jul 00:30 CEST
    [InlineData("2026-03-29", "2026-03-29", "2026-03-28T22:59:00Z", 0)] // 28 Mar 23:59 CET
    [InlineData("2026-03-29", "2026-03-29", "2026-03-28T23:00:00Z", 1)] // 29 Mar 00:00 CET, spring-forward day
    [InlineData("2026-03-29", "2026-03-29", "2026-03-29T21:59:00Z", 1)] // 29 Mar 23:59 CEST
    [InlineData("2026-03-29", "2026-03-29", "2026-03-29T22:00:00Z", 0)] // 30 Mar 00:00 CEST
    [InlineData("2026-10-25", "2026-10-25", "2026-10-24T21:59:00Z", 0)] // 24 Oct 23:59 CEST
    [InlineData("2026-10-25", "2026-10-25", "2026-10-24T22:00:00Z", 1)] // 25 Oct 00:00 CEST, fall-back day
    [InlineData("2026-10-25", "2026-10-25", "2026-10-25T22:59:00Z", 1)] // 25 Oct 23:59 CET
    [InlineData("2026-10-25", "2026-10-25", "2026-10-25T23:00:00Z", 0)] // 26 Oct 00:00 CET
    public async Task GetAsync_ShouldTreatFromAndToAsInclusiveZurichDays(string from, string to, string sentAtUtc, long expected)
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(db, Message(seed, seed.Alice, sentAtUtc));

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, Date(from), Date(to)));

        stats.Totals.Messages.ShouldBe(expected);
        stats.Daily.Sum(d => d.Counts.Messages).ShouldBe(expected);
        stats.Heatmap.Sum(day => day.Sum()).ShouldBe(expected);
    }

    [Fact]
    public async Task GetAsync_ForAUserTrackedOnlyPartOfTheRange_ShouldAverageOverTheirTrackedDays()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        // Tracked 11 to 20 November 2025 (Zurich): 10 of the 30 days of November.
        await AddAsync(db, Period(seed, seed.Alice, "2025-11-10T23:00:00Z", "2025-11-20T23:00:00Z"));
        for (var day = 11; day <= 20; day++)
        {
            await AddAsync(
                db,
                Message(seed, seed.Alice, $"2025-11-{day}T10:00:00Z", length: 4),
                Message(seed, seed.Alice, $"2025-11-{day}T11:00:00Z", length: 6));
        }

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, seed.Alice, new DateOnly(2025, 11, 1), new DateOnly(2025, 11, 30)));

        (stats.TrackedDays, stats.TrackedSince).ShouldBe(((int?)10, (DateOnly?)new DateOnly(2025, 11, 11)));
        stats.Averages.ShouldBe(new ActivityAverages(Messages: 2, Characters: 10, ReactionsGiven: 0, ReactionsReceived: 0, VoiceSeconds: 0));
    }

    [Fact]
    public async Task GetAsync_ForAUserWithAnOpenPeriod_ShouldCountTrackedDaysUntilNow()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        // An earlier closed period, then an open one from 6 January (Zurich); now is 15 January.
        await AddAsync(
            db,
            Period(seed, seed.Alice, "2025-12-01T10:00:00Z", "2025-12-02T10:00:00Z"),
            Period(seed, seed.Alice, "2026-01-05T23:00:00Z", null));
        await AddAsync(db, Message(seed, seed.Alice, "2026-01-12T10:00:00Z"));

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, seed.Alice, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31)));

        (stats.TrackedDays, stats.TrackedSince).ShouldBe(((int?)10, (DateOnly?)new DateOnly(2025, 12, 1)));
        stats.Averages.Messages.ShouldBe(0.1);
    }

    [Fact]
    public async Task GetAsync_ForAUserWithoutATrackingPeriodInTheRange_ShouldReturnAnEmptyResult()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(db, Period(seed, seed.Alice, "2025-11-10T23:00:00Z", "2025-11-20T23:00:00Z"));

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, seed.Alice, _jan10, _jan14));

        stats.TrackedDays.ShouldBe(0);
        stats.Totals.ShouldBe(ActivityCounts.Zero);
        stats.Averages.ShouldBe(ActivityAverages.Zero);
        stats.ActiveDays.ShouldBe(0);
        stats.Daily.Count.ShouldBe(5);
        stats.Daily.ShouldAllBe(d => d.Counts == ActivityCounts.Zero);
        stats.Channels.ShouldBeEmpty();
        stats.Heatmap.Count.ShouldBe(7);
        stats.Heatmap.ShouldAllBe(day => day.All(count => count == 0));
    }

    [Fact]
    public async Task GetAsync_ShouldReturnASevenByTwentyFourHeatmapInZurichTimeMondayFirst()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Message(seed, seed.Alice, "2026-01-12T09:30:00Z"), // Monday 12 Jan 10:30 CET
            Message(seed, seed.Bob, "2026-01-12T09:45:00Z"), // Monday 12 Jan 10:45 CET
            Message(seed, seed.Alice, "2026-01-11T22:30:00Z"), // Sunday 11 Jan 23:30 CET
            Message(seed, seed.Alice, "2026-01-13T23:10:00Z")); // Wednesday 14 Jan 00:10 CET

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, _jan10, _jan14));

        stats.Heatmap.Count.ShouldBe(7);
        stats.Heatmap.ShouldAllBe(day => day.Count == 24);
        stats.Heatmap[0][10].ShouldBe(2);
        stats.Heatmap[6][23].ShouldBe(1);
        stats.Heatmap[2][0].ShouldBe(1);
        stats.Heatmap.Sum(day => day.Sum()).ShouldBe(4);
    }

    [Fact]
    public async Task GetAsync_OnTheFallBackDay_ShouldPutBothPassesOfTheRepeatedHourIntoTwoOClock()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            Message(seed, seed.Alice, "2026-10-25T00:30:00Z"), // Sunday 25 Oct 02:30 CEST
            Message(seed, seed.Alice, "2026-10-25T01:30:00Z")); // Sunday 25 Oct 02:30 CET

        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, null, Date("2026-10-25"), Date("2026-10-25")));

        stats.Heatmap[6][2].ShouldBe(2);
    }

    [Fact]
    public async Task AddActivityStats_ShouldResolveTheServiceOnTheRuntimeRegistration()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(db, Message(seed, seed.Alice, "2026-01-12T10:00:00Z"));
        await Fixture.RefreshContinuousAggregatesAsync(Ct);
        var builder = Fixture.CreateApplicationBuilder();
        builder.Services.AddActivityStats();
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();

        var stats = await scope.ServiceProvider.GetRequiredService<IActivityStatsService>()
            .GetAsync(new ActivityStatsQuery(seed.GuildId, null, _jan10, _jan14), Ct);

        stats.Totals.Messages.ShouldBe(1);
    }

    [Fact]
    public async Task ActivityStatsDto_ShouldRoundTripThroughJson()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(db, Period(seed, seed.Alice, "2026-01-05T23:00:00Z", null), Message(seed, seed.Alice, "2026-01-12T10:00:00Z"));
        var stats = await GetAsync(db, new ActivityStatsQuery(seed.GuildId, seed.Alice, _jan10, _jan14));
        var json = JsonSerializer.Serialize(stats);

        var copy = JsonSerializer.Deserialize<ActivityStatsDto>(json);

        JsonSerializer.Serialize(copy).ShouldBe(json);
        copy!.Heatmap[0][11].ShouldBe(1);
    }

    private async Task<ActivityStatsDto> GetAsync(ApolloDbContext db, ActivityStatsQuery query)
    {
        await Fixture.RefreshContinuousAggregatesAsync(Ct);
        return await new ActivityStatsService(db, TestTime.CreateProvider()).GetAsync(query, Ct);
    }

    private static MessageEvent Message(Seed seed, Guid userId, string atUtc, int length = 10, Guid? channelId = null) =>
        new MessageEventBuilder().In(seed.GuildId, channelId ?? seed.General).By(userId).At(atUtc).WithLength(length).Build();

    private static TrackingPeriod Period(Seed seed, Guid userId, string fromUtc, string? toUtc) => new()
    {
        GuildId = seed.GuildId,
        UserId = userId,
        From = Utc(fromUtc),
        To = toUtc is null ? null : Utc(toUtc),
    };

    private static DateOnly Date(string value) => DateOnly.Parse(value, CultureInfo.InvariantCulture);

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static async Task AddAsync(ApolloDbContext db, params object[] rows)
    {
        db.AddRange(rows);
        await db.SaveChangesAsync(Ct);
    }

    private static async Task<Seed> SeedAsync(ApolloDbContext db)
    {
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var guild = await new GuildBuilder().SeedAsync(db, ids, Ct);
        var alice = await new GuildMemberBuilder().For(TestPlatform.User("alice")).SeedAsync(db, ids, Ct);
        var bob = await new GuildMemberBuilder().For(TestPlatform.User("bob")).SeedAsync(db, ids, Ct);
        var bot = await new GuildMemberBuilder().For(TestPlatform.User("some-bot")).AsBot().SeedAsync(db, ids, Ct);
        var general = await new ChannelBuilder().For(TestPlatform.Channel("general")).Named("general").SeedAsync(db, ids, Ct);
        var thread = await new ChannelBuilder().For(TestPlatform.Channel("thread")).Named("thread").AsThreadOf(general.Id).SeedAsync(db, ids, Ct);
        var lounge = await new ChannelBuilder().For(TestPlatform.Channel("lounge")).Named("lounge").SeedAsync(db, ids, Ct);
        return new Seed(guild.Id, alice.UserId, bob.UserId, bot.UserId, general.Id, thread.Id, lounge.Id);
    }

    private sealed record Seed(Guid GuildId, Guid Alice, Guid Bob, Guid Bot, Guid General, Guid Thread, Guid Lounge);
}
