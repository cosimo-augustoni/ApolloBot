using System.Globalization;
using Apollo.Data;
using Apollo.Data.Activity;
using Apollo.Data.Identity;
using Apollo.Data.Messages;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Apollo.IntegrationTests.Data.Activity;

/// <summary>
/// The activity aggregates (#34): Zurich day and hour buckets, what counts, and how they refresh. Every test seeds its
/// own guild (a new internal ID after the reset) and reads only that guild's rows.
/// </summary>
public sealed class ActivityAggregatesTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly string[] _continuousAggregates =
    [
        "daily_user_channel_messages",
        "daily_user_channel_reactions_given",
        "daily_user_channel_reactions_received",
        "daily_user_channel_voice",
        "hourly_activity_heatmap",
    ];

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData("2026-01-14T22:30:00Z", "2026-01-13T23:00:00Z")] // 14 Jan 23:30 CET
    [InlineData("2026-01-14T22:59:00Z", "2026-01-13T23:00:00Z")] // 14 Jan 23:59 CET
    [InlineData("2026-01-14T23:01:00Z", "2026-01-14T23:00:00Z")] // 15 Jan 00:01 CET
    [InlineData("2026-01-14T23:30:00Z", "2026-01-14T23:00:00Z")] // 15 Jan 00:30 CET
    [InlineData("2026-07-14T21:30:00Z", "2026-07-13T22:00:00Z")] // 14 Jul 23:30 CEST
    [InlineData("2026-07-14T22:30:00Z", "2026-07-14T22:00:00Z")] // 15 Jul 00:30 CEST
    public async Task DailyUserChannelActivity_WhenMessageIsNearZurichMidnight_ShouldCountItOnItsZurichDay(
        string sentAtUtc, string expectedDayUtc)
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(db, new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At(sentAtUtc).Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var row = await db.DailyUserChannelActivity.SingleAsync(a => a.GuildId == seed.GuildId, Ct);
        row.Day.ShouldBe(Utc(expectedDayUtc));
    }

    [Theory]
    [InlineData("2026-03-28T23:30:00Z", "2026-03-28T23:00:00Z")] // 29 Mar 00:30 CET, spring-forward day
    [InlineData("2026-03-29T00:30:00Z", "2026-03-28T23:00:00Z")] // 29 Mar 01:30 CET, before the switch
    [InlineData("2026-03-29T01:30:00Z", "2026-03-28T23:00:00Z")] // 29 Mar 03:30 CEST, after the switch
    [InlineData("2026-03-29T21:59:00Z", "2026-03-28T23:00:00Z")] // 29 Mar 23:59 CEST, last minute of the 23 h day
    [InlineData("2026-03-29T22:30:00Z", "2026-03-29T22:00:00Z")] // 30 Mar 00:30 CEST
    [InlineData("2026-10-24T22:30:00Z", "2026-10-24T22:00:00Z")] // 25 Oct 00:30 CEST, fall-back day
    [InlineData("2026-10-25T00:30:00Z", "2026-10-24T22:00:00Z")] // 25 Oct 02:30 CEST, first pass of the repeated hour
    [InlineData("2026-10-25T01:30:00Z", "2026-10-24T22:00:00Z")] // 25 Oct 02:30 CET, second pass
    [InlineData("2026-10-25T22:30:00Z", "2026-10-24T22:00:00Z")] // 25 Oct 23:30 CET, last hour of the 25 h day
    [InlineData("2026-10-25T23:00:00Z", "2026-10-25T23:00:00Z")] // 26 Oct 00:00 CET
    public async Task DailyUserChannelActivity_WhenMessageIsOnADstSwitchDay_ShouldCountItOnItsZurichDay(
        string sentAtUtc, string expectedDayUtc)
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(db, new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At(sentAtUtc).Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var row = await db.DailyUserChannelActivity.SingleAsync(a => a.GuildId == seed.GuildId, Ct);
        row.Day.ShouldBe(Utc(expectedDayUtc));
    }

    [Theory]
    [InlineData("2026-03-28T23:00:00Z", "2026-03-29T22:00:00Z", 23)] // spring forward
    [InlineData("2026-10-24T22:00:00Z", "2026-10-25T23:00:00Z", 25)] // fall back
    public async Task DailyGuildActivity_OnADstSwitchDay_ShouldCountTheWholeLocalDayInOneBucket(
        string dayStartUtc, string nextDayStartUtc, int hours)
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        var start = Utc(dayStartUtc);
        for (var hour = 0; hour < hours; hour++)
        {
            await AddAsync(db, new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At(start.AddHours(hour).AddMinutes(30)).Build());
        }

        await AddAsync(db, new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At(Utc(nextDayStartUtc)).Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var days = await db.DailyGuildActivity.Where(a => a.GuildId == seed.GuildId).OrderBy(a => a.Day).ToListAsync(Ct);
        days.Select(d => (d.Day, d.MessageCount)).ShouldBe([(start, hours), (Utc(nextDayStartUtc), 1L)]);
    }

    [Fact]
    public async Task DailyUserChannelActivity_ShouldCountCreatedMessagesAndTheirCharactersOnly()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        var deleted = new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).WithLength(12).Build();
        await AddAsync(
            db,
            deleted,
            Repeat(deleted, MessageEventKind.Edit, length: 40, minutesLater: 1),
            Repeat(deleted, MessageEventKind.Delete, length: null, minutesLater: 2),
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).WithLength(30).Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var row = await db.DailyUserChannelActivity.SingleAsync(a => a.GuildId == seed.GuildId, Ct);
        (row.MessageCount, row.CharacterCount).ShouldBe((2L, 42L));
    }

    [Fact]
    public async Task DailyUserChannelActivity_ShouldCountAddedReactionsAsGivenByTheReactorAndReceivedByTheAuthor()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        var onBob = new ReactionEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).OnMessageBy(seed.Bob);
        await AddAsync(
            db,
            onBob.Build(),
            onBob.Build(),
            new ReactionEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).OnMessageBy(seed.Bob).Removed().Build(),
            new ReactionEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).OnMessageBy(null).Build(),
            new ReactionEventBuilder().In(seed.GuildId, seed.General).By(seed.Bob).OnMessageBy(seed.Alice).Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var rows = await db.DailyUserChannelActivity.Where(a => a.GuildId == seed.GuildId).ToListAsync(Ct);
        rows.Select(r => (r.UserId, r.ReactionsGiven, r.ReactionsReceived)).OrderBy(r => r.UserId == seed.Bob)
            .ShouldBe([(seed.Alice, 3L, 1L), (seed.Bob, 1L, 2L)]);
    }

    [Fact]
    public async Task DailyUserChannelActivity_ShouldCountClosedVoiceSessionsWholeOnTheZurichDayTheyStarted()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            new VoiceSessionBuilder().In(seed.GuildId, seed.Lounge).By(seed.Alice).From("2026-01-14T10:00:00Z").Until("2026-01-14T10:20:00Z").Build(),
            new VoiceSessionBuilder().In(seed.GuildId, seed.Lounge).By(seed.Alice).From("2026-01-14T22:30:00Z").Until("2026-01-15T00:30:00Z").Build(),
            new VoiceSessionBuilder().In(seed.GuildId, seed.Lounge).By(seed.Alice).From("2026-01-15T08:00:00Z").Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var rows = await db.DailyUserChannelActivity.Where(a => a.GuildId == seed.GuildId).ToListAsync(Ct);
        rows.Select(r => (r.Day, r.ChannelId, r.VoiceSeconds)).ShouldBe([(Utc("2026-01-13T23:00:00Z"), seed.Lounge, 20 * 60 + 2 * 3600L)]);
    }

    [Fact]
    public async Task DailyUserChannelActivity_WhenABotSendsMessages_ShouldKeepThemMarkedAsBot()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Bot).FromBot().Build(),
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var rows = await db.DailyUserChannelActivity.Where(a => a.GuildId == seed.GuildId).ToListAsync(Ct);
        rows.Select(r => (r.UserId, r.IsBot, r.MessageCount)).OrderBy(r => r.IsBot).ShouldBe([(seed.Alice, false, 1L), (seed.Bot, true, 1L)]);
    }

    [Fact]
    public async Task DailyUserChannelActivity_WhenABotMemberReacts_ShouldMarkTheRowAsBot()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(db, new ReactionEventBuilder().In(seed.GuildId, seed.General).By(seed.Bot).Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var row = await db.DailyUserChannelActivity.SingleAsync(a => a.GuildId == seed.GuildId, Ct);
        row.IsBot.ShouldBeTrue();
    }

    [Fact]
    public async Task DailyUserChannelActivity_WhenActivityIsInAThread_ShouldKeepTheThreadAndCarryItsParent()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            new MessageEventBuilder().In(seed.GuildId, seed.Thread, seed.General).By(seed.Alice).Build(),
            new ReactionEventBuilder().In(seed.GuildId, seed.Thread).By(seed.Bob).OnMessageBy(seed.Alice).Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var rows = await db.DailyUserChannelActivity.Where(a => a.GuildId == seed.GuildId).ToListAsync(Ct);
        rows.Select(r => (r.ChannelId, r.ThreadParentId)).Distinct().ShouldBe([(seed.Thread, (Guid?)seed.General)]);
    }

    [Fact]
    public async Task DailyUserChannelActivity_ShouldReadEveryColumnOfARow()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            new MessageEventBuilder().In(seed.GuildId, seed.Thread, seed.General).By(seed.Alice).WithLength(25).Build(),
            new ReactionEventBuilder().In(seed.GuildId, seed.Thread).By(seed.Alice).Build(),
            new ReactionEventBuilder().In(seed.GuildId, seed.Thread).By(seed.Bob).OnMessageBy(seed.Alice).Build(),
            new VoiceSessionBuilder().In(seed.GuildId, seed.Thread).By(seed.Alice).From(TestTime.Now).Until(TestTime.Now.AddMinutes(1)).Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var row = await db.DailyUserChannelActivity.SingleAsync(a => a.GuildId == seed.GuildId && a.UserId == seed.Alice, Ct);
        row.ShouldBeEquivalentTo(new DailyUserChannelActivity
        {
            Day = Utc("2026-01-14T23:00:00Z"),
            GuildId = seed.GuildId,
            UserId = seed.Alice,
            ChannelId = seed.Thread,
            ThreadParentId = seed.General,
            IsBot = false,
            MessageCount = 1,
            CharacterCount = 25,
            ReactionsGiven = 1,
            ReactionsReceived = 1,
            VoiceSeconds = 60,
        });
    }

    [Fact]
    public async Task DailyGuildActivity_ShouldSumMessagesCharactersAndReactionsPerDayAndBotFlag()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).WithLength(10).Build(),
            new MessageEventBuilder().In(seed.GuildId, seed.Thread, seed.General).By(seed.Bob).WithLength(5).Build(),
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Bot).FromBot().WithLength(100).Build(),
            new ReactionEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).OnMessageBy(seed.Bob).Build(),
            new ReactionEventBuilder().In(seed.GuildId, seed.General).By(seed.Bob).OnMessageBy(seed.Alice).Build(),
            new VoiceSessionBuilder().In(seed.GuildId, seed.Lounge).By(seed.Alice).From(TestTime.Now).Until(TestTime.Now.AddHours(1)).Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var rows = await db.DailyGuildActivity.Where(a => a.GuildId == seed.GuildId).OrderBy(a => a.IsBot).ToListAsync(Ct);
        rows.ShouldBeEquivalentTo(new List<DailyGuildActivity>
        {
            new() { Day = Utc("2026-01-14T23:00:00Z"), GuildId = seed.GuildId, IsBot = false, MessageCount = 2, CharacterCount = 15, ReactionCount = 2 },
            new() { Day = Utc("2026-01-14T23:00:00Z"), GuildId = seed.GuildId, IsBot = true, MessageCount = 1, CharacterCount = 100, ReactionCount = 0 },
        });
    }

    [Fact]
    public async Task HourlyActivityHeatmap_ShouldCountCreatedMessagesPerUserAndHour()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        var first = new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At("2026-01-15T08:05:00Z").Build();
        await AddAsync(
            db,
            first,
            Repeat(first, MessageEventKind.Edit, length: 20, minutesLater: 1),
            new MessageEventBuilder().In(seed.GuildId, seed.Thread, seed.General).By(seed.Alice).At("2026-01-15T08:59:59Z").Build(),
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At("2026-01-15T09:00:00Z").Build(),
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Bob).At("2026-01-15T08:30:00Z").Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var rows = await db.HourlyActivityHeatmap.Where(h => h.GuildId == seed.GuildId).OrderBy(h => h.UserId == seed.Bob).ThenBy(h => h.Hour).ToListAsync(Ct);
        rows.ShouldBeEquivalentTo(new List<HourlyActivityHeatmap>
        {
            new() { Hour = Utc("2026-01-15T08:00:00Z"), GuildId = seed.GuildId, UserId = seed.Alice, IsBot = false, MessageCount = 2 },
            new() { Hour = Utc("2026-01-15T09:00:00Z"), GuildId = seed.GuildId, UserId = seed.Alice, IsBot = false, MessageCount = 1 },
            new() { Hour = Utc("2026-01-15T08:00:00Z"), GuildId = seed.GuildId, UserId = seed.Bob, IsBot = false, MessageCount = 1 },
        });
    }

    [Theory]
    [InlineData("2026-01-15T08:30:00Z", "2026-01-15T09:00:00+01:00")] // winter: Zurich 09:30
    [InlineData("2026-07-15T08:30:00Z", "2026-07-15T10:00:00+02:00")] // summer: Zurich 10:30
    [InlineData("2026-03-29T00:30:00Z", "2026-03-29T01:00:00+01:00")] // spring forward: 01:30 CET
    [InlineData("2026-03-29T01:30:00Z", "2026-03-29T03:00:00+02:00")] // spring forward: 03:30 CEST, 02:00 never happens
    [InlineData("2026-10-25T00:30:00Z", "2026-10-25T02:00:00+02:00")] // fall back: 02:30 CEST, first pass
    [InlineData("2026-10-25T01:30:00Z", "2026-10-25T02:00:00+01:00")] // fall back: 02:30 CET, second pass
    public async Task HourlyActivityHeatmap_ShouldBucketByTheZurichHourOfTheMessage(string sentAtUtc, string expectedZurichHour)
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(db, new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At(sentAtUtc).Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var row = await db.HourlyActivityHeatmap.SingleAsync(h => h.GuildId == seed.GuildId, Ct);
        TimeZoneInfo.ConvertTime(row.Hour, TestTime.Zurich).ShouldBe(DateTimeOffset.Parse(expectedZurichHour, CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task HourlyActivityHeatmap_WhenAFallBackHourRepeats_ShouldKeepBothPassesApart()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        await AddAsync(
            db,
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At("2026-10-25T00:30:00Z").Build(),
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At("2026-10-25T01:30:00Z").Build());

        await Fixture.RefreshContinuousAggregatesAsync(Ct);

        var hours = await db.HourlyActivityHeatmap.Where(h => h.GuildId == seed.GuildId).Select(h => h.Hour).OrderBy(h => h).ToListAsync(Ct);
        hours.Select(h => TimeZoneInfo.ConvertTime(h, TestTime.Zurich).Hour).ShouldBe([2, 2]);
    }

    [Fact]
    public async Task ContinuousAggregates_WhenMigrated_ShouldEachHaveARefreshPolicy()
    {
        await using var db = Fixture.CreateDbContext();

        var policies = await db.Database.SqlQueryRaw<RefreshPolicy>(
                """
                SELECT ca.view_name AS "ViewName",
                       j.schedule_interval AS "ScheduleInterval",
                       (j.config ->> 'start_offset')::interval AS "StartOffset",
                       (j.config ->> 'end_offset')::interval AS "EndOffset"
                FROM timescaledb_information.continuous_aggregates ca
                JOIN timescaledb_information.jobs j
                  ON j.hypertable_schema = ca.view_schema AND j.hypertable_name = ca.view_name
                WHERE j.proc_name = 'policy_refresh_continuous_aggregate' AND ca.view_schema = 'public'
                ORDER BY ca.view_name
                """)
            .ToListAsync(Ct);

        policies.ShouldBe(_continuousAggregates.Select(name => new RefreshPolicy(name, TimeSpan.FromMinutes(30), TimeSpan.FromDays(7), TimeSpan.FromHours(1))));
    }

    [Fact]
    public async Task ContinuousAggregates_WhenMigrated_ShouldUseRealTimeAggregation()
    {
        await using var db = Fixture.CreateDbContext();

        var materializedOnly = await db.Database.SqlQueryRaw<string>(
                """
                SELECT view_name || ':' || materialized_only AS "Value"
                FROM timescaledb_information.continuous_aggregates
                WHERE view_schema = 'public'
                ORDER BY view_name
                """)
            .ToListAsync(Ct);

        materializedOnly.ShouldBe(_continuousAggregates.Select(name => $"{name}:false"));
    }

    /// <summary>
    /// Real-time aggregation reads what no refresh has materialized yet from the raw tables. The event lies after
    /// everything other tests seed, so it is above every aggregate's materialization watermark.
    /// </summary>
    [Fact]
    public async Task Aggregates_WhenNotRefreshed_ShouldStillReturnTheNewestEvents()
    {
        await using var db = Fixture.CreateDbContext();
        var seed = await SeedAsync(db);
        var sentAt = Utc("2036-01-15T09:00:00Z");
        await AddAsync(
            db,
            new MessageEventBuilder().In(seed.GuildId, seed.General).By(seed.Alice).At(sentAt).Build(),
            new ReactionEventBuilder().In(seed.GuildId, seed.General).By(seed.Bob).OnMessageBy(seed.Alice).At(sentAt).Build(),
            new VoiceSessionBuilder().In(seed.GuildId, seed.Lounge).By(seed.Alice).From(sentAt).Until(sentAt.AddMinutes(5)).Build());

        var activity = await db.DailyUserChannelActivity.Where(a => a.GuildId == seed.GuildId).SumAsync(a => a.MessageCount + a.ReactionsGiven + a.ReactionsReceived + a.VoiceSeconds, Ct);
        var heatmap = await db.HourlyActivityHeatmap.Where(h => h.GuildId == seed.GuildId).SumAsync(h => h.MessageCount, Ct);

        (activity, heatmap).ShouldBe((1L + 1 + 1 + 300, 1L));
    }

    [Fact]
    public async Task Model_WhenAllMigrationsAreApplied_ShouldHaveNoPendingChanges()
    {
        await using var db = Fixture.CreateDbContext();

        db.Database.HasPendingModelChanges().ShouldBeFalse();
    }

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private static MessageEvent Repeat(MessageEvent create, MessageEventKind kind, int? length, int minutesLater) => new()
    {
        GuildId = create.GuildId,
        UserId = create.UserId,
        ChannelId = create.ChannelId,
        ThreadParentId = create.ThreadParentId,
        OccurredAt = create.OccurredAt.AddMinutes(minutesLater),
        MessageExtId = create.MessageExtId,
        Kind = kind,
        Length = length,
        IsBot = create.IsBot,
    };

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

    private sealed record RefreshPolicy(string ViewName, TimeSpan ScheduleInterval, TimeSpan StartOffset, TimeSpan EndOffset);
}
