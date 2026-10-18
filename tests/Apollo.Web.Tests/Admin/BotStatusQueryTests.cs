using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Status;
using Apollo.Data.Tracking;
using Apollo.Data.Voice;
using Apollo.Domain.Voice;
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
/// The bot status of the admin page (#31, #91): the latest heartbeat of the configured guild and its stale rule, and what
/// the database says about it: open voice sessions, the newest recorded row, the last connection, the latency mean and
/// the tracking mode.
/// </summary>
public sealed class BotStatusQueryTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly PlatformRef _otherGuild = new(PlatformKind.Discord, "333333333333333333");

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task BotStatusQuery_WhenHeartbeatRecent_ShouldReturnStatusNotStale()
    {
        var lastEventAt = TestTime.Now - TimeSpan.FromSeconds(42);
        await SeedHeartbeatAsync(TestTime.Now - TimeSpan.FromMinutes(1), connected: false, latencyMs: null, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now - TimeSpan.FromSeconds(30), connected: true, latencyMs: 84, lastEventAt, openVoiceSessions: 3);

        var status = await QueryAsync();

        status.CheckedAt.ShouldBe(TestTime.Now);
        status.HasHeartbeat.ShouldBeTrue();
        status.LastHeartbeatAt.ShouldBe(TestTime.Now - TimeSpan.FromSeconds(30));
        status.Connected.ShouldBeTrue();
        status.GatewayLatencyMeanMs.ShouldBe(84);
        status.IsStale.ShouldBeFalse();
    }

    [Theory]
    [InlineData(120, false)]
    [InlineData(121, true)]
    [InlineData(119, false)]
    public async Task BotStatusQuery_WhenHeartbeatAged_ShouldBeStaleOnlyAfterTwoMinutes(int ageSeconds, bool expectedStale)
    {
        await SeedHeartbeatAsync(TestTime.Now - TimeSpan.FromSeconds(ageSeconds), connected: true, latencyMs: 84, lastEventAt: null, openVoiceSessions: 0);

        var status = await QueryAsync();

        status.IsStale.ShouldBe(expectedStale);
    }

    [Fact]
    public async Task BotStatusQuery_WhenHeartbeatOlderThanTwoMinutes_ShouldBeStale()
    {
        await SeedHeartbeatAsync(TestTime.Now - TimeSpan.FromMinutes(28), connected: true, latencyMs: 84, lastEventAt: null, openVoiceSessions: 0);

        var status = await QueryAsync();

        status.IsStale.ShouldBeTrue();
        status.LastHeartbeatAt.ShouldBe(TestTime.Now - TimeSpan.FromMinutes(28));
    }

    [Fact]
    public async Task BotStatusQuery_WhenStaleAfterConfigured_ShouldUseIt()
    {
        await SeedHeartbeatAsync(TestTime.Now - TimeSpan.FromMinutes(3), connected: true, latencyMs: 84, lastEventAt: null, openVoiceSessions: 0);

        var status = await QueryAsync(new BotStatusOptions { StaleAfter = TimeSpan.FromMinutes(5) });

        status.IsStale.ShouldBeFalse();
    }

    [Fact]
    public async Task BotStatusQuery_WhenNoHeartbeat_ShouldReturnNoHeartbeatState()
    {
        var status = await QueryAsync();

        status.HasHeartbeat.ShouldBeFalse();
        status.LastHeartbeatAt.ShouldBeNull();
        status.IsStale.ShouldBeFalse();
        status.CheckedAt.ShouldBe(TestTime.Now);
    }

    [Fact]
    public async Task BotStatusQuery_WhenOnlyAnotherGuildHasHeartbeats_ShouldReturnNoHeartbeatState()
    {
        await SeedHeartbeatAsync(TestTime.Now - TimeSpan.FromSeconds(30), connected: true, latencyMs: 84, lastEventAt: null, openVoiceSessions: 0,
            guild: new PlatformRef(PlatformKind.Discord, "333333333333333333"));

        var status = await QueryAsync();

        status.HasHeartbeat.ShouldBeFalse();
    }

    [Fact]
    public async Task BotStatusQuery_WhenOpenVoiceSessionsInTwoChannels_ShouldListThemOldestFirstWithMemberAndChannel()
    {
        var events = new EventSeeder(Fixture, Ct);
        var anna = await events.UserAsync(TestPlatform.User("anna"), "Anna");
        var ben = await events.UserAsync(TestPlatform.User("ben"), "Ben");
        var chris = await events.UserAsync(TestPlatform.User("chris"), "Chris");
        var lounge = await events.ChannelAsync(TestPlatform.Channel("lounge"), "Voice-Lounge");
        var gaming = await events.ChannelAsync(TestPlatform.Channel("gaming"), "Gaming");
        await events.AddAsync(DataEventType.VoiceSession, TestTime.Now.AddMinutes(-15), chris, gaming);
        await events.AddAsync(DataEventType.VoiceSession, TestTime.Now.AddMinutes(-50), anna, lounge);
        await events.AddAsync(DataEventType.VoiceSession, TestTime.Now.AddMinutes(-18), ben, lounge);

        var status = await QueryAsync();

        status.OpenVoiceSessions.ShouldBe(
        [
            new OpenVoiceSessionDto("Anna", lounge, "Voice-Lounge", TestTime.Now.AddMinutes(-50)),
            new OpenVoiceSessionDto("Ben", lounge, "Voice-Lounge", TestTime.Now.AddMinutes(-18)),
            new OpenVoiceSessionDto("Chris", gaming, "Gaming", TestTime.Now.AddMinutes(-15)),
        ]);
    }

    [Fact]
    public async Task BotStatusQuery_WhenVoiceSessionsClosedOrOfAnotherGuild_ShouldNotListThem()
    {
        var events = new EventSeeder(Fixture, Ct);
        var anna = await events.UserAsync(TestPlatform.User("anna"), "Anna");
        await SeedClosedVoiceSessionAsync(anna, TestTime.Now.AddMinutes(-40), TestTime.Now.AddMinutes(-10));
        await events.AddAsync(DataEventType.VoiceSession, TestTime.Now.AddMinutes(-5), anna, guild: _otherGuild);

        var status = await QueryAsync();

        status.OpenVoiceSessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task BotStatusQuery_WhenEventsInSeveralTables_ShouldReturnTheNewestRowWithItsChannel()
    {
        var events = new EventSeeder(Fixture, Ct);
        var general = await events.ChannelAsync(EventSeeder.General, "allgemein");
        await events.AddAsync(DataEventType.PresenceActivity, TestTime.Now.AddMinutes(-40));
        await events.AddAsync(DataEventType.VoiceSession, TestTime.Now.AddMinutes(-30));
        await events.AddAsync(DataEventType.Membership, TestTime.Now.AddMinutes(-20));
        await events.AddAsync(DataEventType.Message, TestTime.Now.AddMinutes(-3), channelId: general);
        await events.AddAsync(DataEventType.Reaction, TestTime.Now.AddMinutes(-10), channelId: general);
        await events.AddAsync(DataEventType.Message, TestTime.Now.AddMinutes(-1), guild: _otherGuild);

        var status = await QueryAsync();

        status.LastRecordedEvent.ShouldBe(new LastRecordedEventDto(DataEventType.Message, TestTime.Now.AddMinutes(-3), "allgemein"));
    }

    [Theory]
    [InlineData(DataEventType.Membership)]
    [InlineData(DataEventType.PresenceActivity)]
    public async Task BotStatusQuery_WhenTheNewestRowHasNoChannel_ShouldReturnItWithoutChannel(DataEventType type)
    {
        var events = new EventSeeder(Fixture, Ct);
        await events.AddAsync(DataEventType.Message, TestTime.Now.AddMinutes(-3));
        await events.AddAsync(type, TestTime.Now.AddMinutes(-2));

        var status = await QueryAsync();

        status.LastRecordedEvent.ShouldBe(new LastRecordedEventDto(type, TestTime.Now.AddMinutes(-2), null));
    }

    [Fact]
    public async Task BotStatusQuery_WhenTheNewestRowIsAVoiceSession_ShouldReturnItsChannel()
    {
        var events = new EventSeeder(Fixture, Ct);
        var lounge = await events.ChannelAsync(TestPlatform.Channel("lounge"), "Voice-Lounge");
        await events.AddAsync(DataEventType.VoiceSession, TestTime.Now.AddMinutes(-2), channelId: lounge);

        var status = await QueryAsync();

        status.LastRecordedEvent.ShouldBe(new LastRecordedEventDto(DataEventType.VoiceSession, TestTime.Now.AddMinutes(-2), "Voice-Lounge"));
    }

    [Fact]
    public async Task BotStatusQuery_WhenNothingRecorded_ShouldReturnNoLastRecordedEventAndNoVoiceSessions()
    {
        await SeedHeartbeatAsync(TestTime.Now - TimeSpan.FromSeconds(30), connected: true, latencyMs: 84, lastEventAt: null, openVoiceSessions: 0);

        var status = await QueryAsync();

        status.LastRecordedEvent.ShouldBeNull();
        status.OpenVoiceSessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task BotStatusQuery_WhenDisconnected_ShouldReturnTheLatestHeartbeatThatSaidConnected()
    {
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-10), connected: true, latencyMs: 84, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-6), connected: true, latencyMs: 84, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-5), connected: false, latencyMs: null, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddSeconds(-30), connected: false, latencyMs: null, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-1), connected: true, latencyMs: 84, lastEventAt: null, openVoiceSessions: 0, guild: _otherGuild);

        var status = await QueryAsync();

        status.Connected.ShouldBeFalse();
        status.LastConnectedAt.ShouldBe(TestTime.Now.AddMinutes(-6));
    }

    [Fact]
    public async Task BotStatusQuery_WhenNeverConnected_ShouldReturnNoLastConnected()
    {
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-5), connected: false, latencyMs: null, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddSeconds(-30), connected: false, latencyMs: null, lastEventAt: null, openVoiceSessions: 0);

        var status = await QueryAsync();

        status.LastConnectedAt.ShouldBeNull();
    }

    [Fact]
    public async Task BotStatusQuery_WhenHeartbeatsInTheLastFiveMinutes_ShouldReturnTheirMeanLatencyRounded()
    {
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-6), connected: true, latencyMs: 1000, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-5).AddSeconds(-1), connected: true, latencyMs: 1000, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-4), connected: true, latencyMs: 80, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-3), connected: true, latencyMs: null, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-2), connected: true, latencyMs: 85, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddSeconds(-30), connected: true, latencyMs: 86, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-1), connected: true, latencyMs: 1000, lastEventAt: null, openVoiceSessions: 0, guild: _otherGuild);

        var status = await QueryAsync();

        status.GatewayLatencyMeanMs.ShouldBe(84);
    }

    [Fact]
    public async Task BotStatusQuery_WhenNoLatencyInTheLastFiveMinutes_ShouldReturnNoLatency()
    {
        await SeedHeartbeatAsync(TestTime.Now.AddMinutes(-6), connected: true, latencyMs: 84, lastEventAt: null, openVoiceSessions: 0);
        await SeedHeartbeatAsync(TestTime.Now.AddSeconds(-30), connected: false, latencyMs: null, lastEventAt: null, openVoiceSessions: 0);

        var status = await QueryAsync();

        status.GatewayLatencyMeanMs.ShouldBeNull();
    }

    [Theory]
    [InlineData("none", true)]
    [InlineData("set", false)]
    [InlineData("deleted", false)]
    public async Task BotStatusQuery_ShouldReportNoTrackingRoleOnlyWhileNoneIsSet(string trackingRole, bool expected)
    {
        await SeedSyncedGuildAsync(trackingRole);

        var status = await QueryAsync();

        status.NoTrackingRole.ShouldBe(expected);
    }

    [Fact]
    public async Task BotStatusQuery_WhenTheGuildIsNotSyncedYet_ShouldNotReportNoTrackingRole()
    {
        await SeedHeartbeatAsync(TestTime.Now - TimeSpan.FromSeconds(30), connected: true, latencyMs: 84, lastEventAt: null, openVoiceSessions: 0);

        var status = await QueryAsync();

        status.NoTrackingRole.ShouldBeFalse();
    }

    private async Task<BotStatusDto> QueryAsync(BotStatusOptions? options = null)
    {
        await using var db = Fixture.CreateDbContext();
        var query = new BotStatusQuery(db, [new FakeIdentityProvider()], new RoleSettingsQuery(db), Options.Create(options ?? new BotStatusOptions()), _time);
        return await query.GetAsync(Ct);
    }

    private async Task SeedHeartbeatAsync(
        DateTimeOffset at, bool connected, int? latencyMs, DateTimeOffset? lastEventAt, int openVoiceSessions, PlatformRef? guild = null)
    {
        await using var db = Fixture.CreateDbContext();
        var guildId = await Ids(db).ResolveGuildAsync(guild ?? ApolloWebApplicationFactory.Guild, Ct);
        db.BotStatuses.Add(new BotStatus
        {
            GuildId = guildId,
            OccurredAt = at,
            Connected = connected,
            GatewayLatencyMs = latencyMs,
            LastEventAt = lastEventAt,
            OpenVoiceSessions = openVoiceSessions,
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task SeedClosedVoiceSessionAsync(Guid userId, DateTimeOffset startedAt, DateTimeOffset endedAt)
    {
        await using var db = Fixture.CreateDbContext();
        var ids = Ids(db);
        db.VoiceSessions.Add(new VoiceSession
        {
            GuildId = await ids.ResolveGuildAsync(ApolloWebApplicationFactory.Guild, Ct),
            UserId = userId,
            ChannelId = await ids.ResolveChannelAsync(ApolloWebApplicationFactory.Guild, EventSeeder.General, Ct),
            OccurredAt = startedAt,
            EndedAt = endedAt,
            EndReason = VoiceSessionEndReason.Left,
        });
        await db.SaveChangesAsync(Ct);
    }

    private async Task SeedSyncedGuildAsync(string trackingRole)
    {
        await using var db = Fixture.CreateDbContext();
        var ids = Ids(db);
        var guild = ApolloWebApplicationFactory.Guild;
        await new GuildBuilder().For(guild).Named("Apollo").SeedAsync(db, ids, Ct);
        var role = new RoleBuilder().For(TestPlatform.Role("statistik"), guild).Named("Statistik");
        await (trackingRole == "deleted" ? role.DeletedAt(TestTime.Now.AddDays(-1)) : role).SeedAsync(db, ids, Ct);
        if (trackingRole != "none")
        {
            await new RoleSettingBuilder().ForRole(TestPlatform.Role("statistik"), guild).SeedAsync(db, ids, Ct);
        }
    }

    private static PlatformIdResolver Ids(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
}
