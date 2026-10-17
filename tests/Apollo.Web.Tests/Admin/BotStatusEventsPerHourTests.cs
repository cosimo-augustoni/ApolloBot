using Apollo.Data.Identity;
using Apollo.Data.Membership;
using Apollo.Data.Messages;
using Apollo.Data.Presence;
using Apollo.Data.Reactions;
using Apollo.Data.Voice;
using Apollo.Domain.Presence;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Apollo.Web.Admin;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Tests.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Tests.Admin;

/// <summary>
/// The events per hour of the admin bot status page (#31): rows of the raw event tables in the last 24
/// <c>Europe/Zurich</c> hours, every hour present.
/// </summary>
public sealed class BotStatusEventsPerHourTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    // 10:20 in Zurich (UTC+1): the current hour starts at 09:00 UTC, the first of the 24 at 10:00 UTC the day before.
    private static readonly DateTimeOffset _now = TestTime.Now.AddMinutes(20);
    private static readonly DateTimeOffset _currentHour = TestTime.Now;
    private static readonly DateTimeOffset _firstHour = _currentHour.AddHours(-23);

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task EventsPerHour_ShouldReturn24ZurichBucketsWithZeroFill()
    {
        _time.SetUtcNow(_now);
        await SeedAsync(EventTable.Message, _currentHour.AddSeconds(-1));
        await SeedAsync(EventTable.Message, _currentHour);
        await SeedAsync(EventTable.Reaction, _currentHour.AddMinutes(5));
        await SeedAsync(EventTable.Membership, _currentHour.AddMinutes(10));
        await SeedAsync(EventTable.Voice, _currentHour.AddMinutes(15));
        await SeedAsync(EventTable.Presence, _currentHour.AddMinutes(19));
        await SeedAsync(EventTable.Reaction, _firstHour.AddMinutes(30));

        var status = await QueryAsync();

        status.EventsPerHour.Count.ShouldBe(24);
        status.EventsPerHour.Select(h => h.HourStart).ShouldBe(Enumerable.Range(0, 24).Select(i => _firstHour.AddHours(i)));
        status.EventsPerHour[^1].ShouldBe(new HourlyEventCountDto(_currentHour, 5));
        status.EventsPerHour[^2].ShouldBe(new HourlyEventCountDto(_currentHour.AddHours(-1), 1));
        status.EventsPerHour[0].ShouldBe(new HourlyEventCountDto(_firstHour, 1));
        status.EventsPerHour.Skip(1).SkipLast(2).ShouldAllBe(h => h.Count == 0);
    }

    [Fact]
    public async Task EventsPerHour_WhenEventsOlderThan24Hours_ShouldNotCountThem()
    {
        _time.SetUtcNow(_now);
        await SeedAsync(EventTable.Message, _now.AddHours(-24));
        await SeedAsync(EventTable.Voice, _firstHour.AddSeconds(-1));
        await SeedAsync(EventTable.Presence, _now.AddDays(-3));
        await SeedAsync(EventTable.Membership, _firstHour);

        var status = await QueryAsync();

        status.EventsPerHour.Sum(h => h.Count).ShouldBe(1);
        status.EventsPerHour[0].Count.ShouldBe(1);
    }

    [Fact]
    public async Task EventsPerHour_WhenEventsOfAnotherGuild_ShouldNotCountThem()
    {
        _time.SetUtcNow(_now);
        await SeedAsync(EventTable.Message, _currentHour.AddMinutes(1), new PlatformRef(PlatformKind.Discord, "333333333333333333"));

        var status = await QueryAsync();

        status.EventsPerHour.Count.ShouldBe(24);
        status.EventsPerHour.ShouldAllBe(h => h.Count == 0);
    }

    [Fact]
    public async Task EventsPerHour_WhenWindowSpansTheEndOfSummerTime_ShouldCountBothZurichHoursFromTwoToThree()
    {
        // Sunday 25 October 2026: at 03:00 CEST (01:00 UTC) the clocks go back to 02:00 CET, so 02:xx happens twice.
        var firstTwoAm = new DateTimeOffset(2026, 10, 25, 0, 0, 0, TimeSpan.Zero);
        _time.SetUtcNow(new DateTimeOffset(2026, 10, 25, 12, 30, 0, TimeSpan.Zero));
        await SeedAsync(EventTable.Message, firstTwoAm.AddMinutes(30));
        await SeedAsync(EventTable.Message, firstTwoAm.AddHours(1).AddMinutes(30));
        await SeedAsync(EventTable.Message, firstTwoAm.AddHours(1).AddMinutes(45));

        var status = await QueryAsync();

        status.EventsPerHour.Count.ShouldBe(24);
        status.EventsPerHour[0].HourStart.ShouldBe(new DateTimeOffset(2026, 10, 24, 13, 0, 0, TimeSpan.Zero));
        status.EventsPerHour.Zip(status.EventsPerHour.Skip(1)).ShouldAllBe(p => p.Second.HourStart - p.First.HourStart == TimeSpan.FromHours(1));
        status.EventsPerHour.Single(h => h.HourStart == firstTwoAm).Count.ShouldBe(1);
        status.EventsPerHour.Single(h => h.HourStart == firstTwoAm.AddHours(1)).Count.ShouldBe(2);
    }

    private async Task<BotStatusDto> QueryAsync()
    {
        await using var db = Fixture.CreateDbContext();
        var query = new BotStatusQuery(db, [new FakeIdentityProvider()], Options.Create(new BotStatusOptions()), _time);
        return await query.GetAsync(Ct);
    }

    private enum EventTable
    {
        Message,
        Reaction,
        Membership,
        Voice,
        Presence,
    }

    private async Task SeedAsync(EventTable table, DateTimeOffset at, PlatformRef? guild = null)
    {
        await using var db = Fixture.CreateDbContext();
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var guildRef = guild ?? ApolloWebApplicationFactory.Guild;
        var guildId = await ids.ResolveGuildAsync(guildRef, Ct);
        var userId = await ids.ResolveUserAsync(TestPlatform.User("alice"), Ct);
        var channelId = await ids.ResolveChannelAsync(guildRef, TestPlatform.Channel("general"), Ct);

        switch (table)
        {
            case EventTable.Message:
                db.MessageEvents.Add(new MessageEvent
                {
                    GuildId = guildId, UserId = userId, ChannelId = channelId, OccurredAt = at, MessageExtId = "m", Kind = MessageEventKind.Create,
                });
                break;
            case EventTable.Reaction:
                db.ReactionEvents.Add(new ReactionEvent
                {
                    GuildId = guildId, UserId = userId, ChannelId = channelId, OccurredAt = at, MessageExtId = "m", EmojiName = "👍", Kind = ReactionEventKind.Add,
                });
                break;
            case EventTable.Membership:
                db.MembershipEvents.Add(new MembershipEvent { GuildId = guildId, UserId = userId, OccurredAt = at, Kind = MembershipEventKind.Join });
                break;
            case EventTable.Voice:
                db.VoiceSessions.Add(new VoiceSession { GuildId = guildId, UserId = userId, ChannelId = channelId, OccurredAt = at });
                break;
            case EventTable.Presence:
                db.PresenceActivitySessions.Add(new PresenceActivitySession
                {
                    GuildId = guildId, UserId = userId, OccurredAt = at, EndedAt = at.AddMinutes(1), ActivityType = PresenceActivityType.Playing, ActivityName = "Factorio",
                });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(table), table, null);
        }

        await db.SaveChangesAsync(Ct);
    }
}
