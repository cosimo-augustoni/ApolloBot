using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Status;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Web.Admin;
using Apollo.Web.Tests.Auth;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Tests.Admin;

/// <summary>The bot status of the admin page (#31): the latest heartbeat of the configured guild and its stale rule.</summary>
public sealed class BotStatusQueryTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
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
        status.GatewayLatencyMs.ShouldBe(84);
        status.LastEventAt.ShouldBe(lastEventAt);
        status.OpenVoiceSessions.ShouldBe(3);
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

    private async Task<Client.Contracts.BotStatusDto> QueryAsync(BotStatusOptions? options = null)
    {
        await using var db = Fixture.CreateDbContext();
        var query = new BotStatusQuery(db, [new FakeIdentityProvider()], Options.Create(options ?? new BotStatusOptions()), _time);
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

    private static PlatformIdResolver Ids(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
}
