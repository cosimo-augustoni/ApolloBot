using Apollo.Data.Guilds;
using Apollo.Data.Identity;
using Apollo.Data.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Apollo.Web.Admin;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Tests.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Tests.Admin;

/// <summary>
/// The overview of the admin data page (#32): rows per raw event table and <c>Europe/Zurich</c> day.
/// </summary>
public sealed class DataOverviewQueryTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private static readonly DateOnly _jan13 = new(2026, 1, 13);
    private static readonly DateOnly _jan14 = new(2026, 1, 14);
    private static readonly DateOnly _jan15 = new(2026, 1, 15);

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private EventSeeder Events => new(Fixture, Ct);

    [Fact]
    public async Task Counts_ShouldReturnTotalsPerTableAndDay()
    {
        // TestTime.Now is 15 January 2026, 10:00 in Zurich (UTC+1).
        await Events.AddAsync(DataEventType.Message, Utc(2026, 1, 13, 8));
        await Events.AddAsync(DataEventType.Message, Utc(2026, 1, 13, 9));
        await Events.AddAsync(DataEventType.Message, Utc(2026, 1, 15, 8));
        await Events.AddAsync(DataEventType.VoiceSession, Utc(2026, 1, 14, 12));
        await Events.AddAsync(DataEventType.VoiceStateSegment, Utc(2026, 1, 14, 12));
        await Events.AddAsync(DataEventType.VoiceStateSegment, Utc(2026, 1, 14, 13));
        await Events.AddAsync(DataEventType.Reaction, Utc(2026, 1, 15, 7));
        await Events.AddAsync(DataEventType.Membership, Utc(2026, 1, 13, 20));
        await Events.AddAsync(DataEventType.PresenceActivity, Utc(2026, 1, 15, 6));
        await Events.AddAsync(DataEventType.PresenceActivitySegment, Utc(2026, 1, 14, 6));
        await Events.AddAsync(DataEventType.PresenceActivitySegment, Utc(2026, 1, 15, 6));
        await Events.AddAsync(DataEventType.MemberName, Utc(2026, 1, 13, 6));
        await Events.AddAsync(DataEventType.Message, Utc(2026, 1, 12, 22));
        await Events.AddAsync(DataEventType.Reaction, Utc(2026, 1, 15, 23));
        await Events.AddAsync(DataEventType.Message, Utc(2026, 1, 14, 8), guild: new PlatformRef(PlatformKind.Discord, "333333333333333333"));

        var counts = await QueryAsync(_jan13, _jan15);

        counts.From.ShouldBe(_jan13);
        counts.To.ShouldBe(_jan15);
        counts.Tables.Select(t => t.Type).ShouldBe(Enum.GetValues<DataEventType>());
        Days(counts, DataEventType.Message).ShouldBe([(_jan13, 2), (_jan14, 0), (_jan15, 1)]);
        Days(counts, DataEventType.VoiceSession).ShouldBe([(_jan13, 0), (_jan14, 1), (_jan15, 0)]);
        Days(counts, DataEventType.VoiceStateSegment).ShouldBe([(_jan13, 0), (_jan14, 2), (_jan15, 0)]);
        Days(counts, DataEventType.Reaction).ShouldBe([(_jan13, 0), (_jan14, 0), (_jan15, 1)]);
        Days(counts, DataEventType.Membership).ShouldBe([(_jan13, 1), (_jan14, 0), (_jan15, 0)]);
        Days(counts, DataEventType.PresenceActivity).ShouldBe([(_jan13, 0), (_jan14, 0), (_jan15, 1)]);
        Days(counts, DataEventType.PresenceActivitySegment).ShouldBe([(_jan13, 0), (_jan14, 1), (_jan15, 1)]);
        Days(counts, DataEventType.MemberName).ShouldBe([(_jan13, 1), (_jan14, 0), (_jan15, 0)]);
        counts.Tables.Select(t => t.Total).ShouldBe([3, 1, 2, 1, 1, 1, 2, 1]);
    }

    [Fact]
    public async Task Counts_ShouldBucketByZurichDayIncludingDst()
    {
        // Winter: 23:30 UTC is 00:30 the next day in Zurich.
        await Events.AddAsync(DataEventType.Message, new DateTimeOffset(2026, 1, 13, 23, 30, 0, TimeSpan.Zero));
        // Sunday 29 March 2026 has 23 hours in Zurich: 28 March 23:00 UTC to 29 March 22:00 UTC.
        await Events.AddAsync(DataEventType.Message, new DateTimeOffset(2026, 3, 28, 22, 59, 0, TimeSpan.Zero));
        await Events.AddAsync(DataEventType.Message, new DateTimeOffset(2026, 3, 28, 23, 0, 0, TimeSpan.Zero));
        await Events.AddAsync(DataEventType.Message, new DateTimeOffset(2026, 3, 29, 21, 59, 0, TimeSpan.Zero));
        await Events.AddAsync(DataEventType.Message, new DateTimeOffset(2026, 3, 29, 22, 0, 0, TimeSpan.Zero));
        // Sunday 25 October 2026 has 25 hours: 24 October 22:00 UTC to 25 October 23:00 UTC.
        await Events.AddAsync(DataEventType.Reaction, new DateTimeOffset(2026, 10, 24, 22, 0, 0, TimeSpan.Zero));
        await Events.AddAsync(DataEventType.Reaction, new DateTimeOffset(2026, 10, 25, 22, 59, 0, TimeSpan.Zero));
        await Events.AddAsync(DataEventType.Reaction, new DateTimeOffset(2026, 10, 25, 23, 0, 0, TimeSpan.Zero));

        var winter = await QueryAsync(_jan13, _jan14);
        var spring = await QueryAsync(new DateOnly(2026, 3, 28), new DateOnly(2026, 3, 30));
        var autumn = await QueryAsync(new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 26));

        Days(winter, DataEventType.Message).ShouldBe([(_jan13, 0), (_jan14, 1)]);
        Days(spring, DataEventType.Message).ShouldBe([(new DateOnly(2026, 3, 28), 1), (new DateOnly(2026, 3, 29), 2), (new DateOnly(2026, 3, 30), 1)]);
        Days(autumn, DataEventType.Reaction).ShouldBe([(new DateOnly(2026, 10, 24), 0), (new DateOnly(2026, 10, 25), 2), (new DateOnly(2026, 10, 26), 1)]);
    }

    [Fact]
    public void CountsRange_WhenNotGiven_ShouldBeTheLast30ZurichDaysIncludingToday()
    {
        // 23:30 UTC on 15 January is already 16 January in Zurich.
        _time.SetUtcNow(new DateTimeOffset(2026, 1, 15, 23, 30, 0, TimeSpan.Zero));

        var range = CreateQuery(null!).ResolveCountsRange(null, null);

        range.ShouldBe((new DateOnly(2025, 12, 18), new DateOnly(2026, 1, 16)));
    }

    [Fact]
    public void CountsRange_WhenOnlyFromGiven_ShouldEndToday()
    {
        var range = CreateQuery(null!).ResolveCountsRange(_jan13, null);

        range.ShouldBe((_jan13, _jan15));
    }

    [Fact]
    public async Task MemberTracking_WhenTrackingRoleConfigured_ShouldCountRoleHolders()
    {
        await SeedMembersAsync(trackingRole: "set");

        var tracking = await MemberTrackingAsync();

        tracking.ShouldBe([new MemberTrackingDto("Discord", Tracked: 2, Untracked: 2, Total: 4, RoleSettingsMode.Active)]);
    }

    [Fact]
    public async Task MemberTracking_WhenNoTrackingRole_ShouldReportZeroTracked()
    {
        await SeedMembersAsync(trackingRole: "none");

        var tracking = await MemberTrackingAsync();

        tracking.ShouldBe([new MemberTrackingDto("Discord", Tracked: 0, Untracked: 4, Total: 4, RoleSettingsMode.NoTrackingRole)]);
    }

    [Fact]
    public async Task MemberTracking_WhenTrackingRoleDeleted_ShouldReportZeroTracked()
    {
        await SeedMembersAsync(trackingRole: "deleted");

        var tracking = await MemberTrackingAsync();

        tracking.ShouldBe([new MemberTrackingDto("Discord", Tracked: 0, Untracked: 4, Total: 4, RoleSettingsMode.TrackingRoleDeleted)]);
    }

    [Fact]
    public async Task MemberTracking_WhenGuildNotSyncedYet_ShouldReturnNoPlatform()
    {
        var tracking = await MemberTrackingAsync();

        tracking.ShouldBeEmpty();
    }

    /// <summary>
    /// The configured guild, synced: Alice and Bob hold Statistik, Carol holds Mitglied, Dana no role, a bot and a
    /// departed member hold Statistik too, and Erin holds it in another guild.
    /// </summary>
    private async Task SeedMembersAsync(string trackingRole)
    {
        await using var db = Fixture.CreateDbContext();
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var guildRef = ApolloWebApplicationFactory.Guild;
        var otherGuild = new PlatformRef(PlatformKind.Discord, "333333333333333333");
        await new GuildBuilder().For(guildRef).Named("Apollo").SeedAsync(db, ids, Ct);
        var statistik = await new RoleBuilder().For(TestPlatform.Role("statistik"), guildRef).Named("Statistik").SeedAsync(db, ids, Ct);
        var mitglied = await new RoleBuilder().For(TestPlatform.Role("mitglied"), guildRef).Named("Mitglied").SeedAsync(db, ids, Ct);
        var foreign = await new RoleBuilder().For(TestPlatform.Role("foreign"), otherGuild).SeedAsync(db, ids, Ct);

        async Task MemberAsync(GuildMemberBuilder member, params Guid[] roleIds)
        {
            var row = await member.SeedAsync(db, ids, Ct);
            db.MemberRoles.AddRange(roleIds.Select(roleId => new MemberRole { GuildId = row.GuildId, UserId = row.UserId, RoleId = roleId }));
            await db.SaveChangesAsync(Ct);
        }

        await MemberAsync(new GuildMemberBuilder().For(TestPlatform.User("alice"), guildRef), statistik.Id, mitglied.Id);
        await MemberAsync(new GuildMemberBuilder().For(TestPlatform.User("bob"), guildRef).AsAdministrator(), statistik.Id);
        await MemberAsync(new GuildMemberBuilder().For(TestPlatform.User("carol"), guildRef).AsAdministrator(), mitglied.Id);
        await MemberAsync(new GuildMemberBuilder().For(TestPlatform.User("dana"), guildRef));
        await MemberAsync(new GuildMemberBuilder().For(TestPlatform.User("apollo-bot"), guildRef).AsBot(), statistik.Id);
        await MemberAsync(new GuildMemberBuilder().For(TestPlatform.User("gone"), guildRef).LeftAt(TestTime.Now), statistik.Id);
        await MemberAsync(new GuildMemberBuilder().For(TestPlatform.User("erin"), otherGuild), foreign.Id);

        if (trackingRole != "none")
        {
            await new RoleSettingBuilder().ForRole(TestPlatform.Role("statistik"), guildRef).SeedAsync(db, ids, Ct);
        }

        if (trackingRole == "deleted")
        {
            await db.Roles.Where(r => r.Id == statistik.Id).ExecuteUpdateAsync(s => s.SetProperty(r => r.DeletedAt, TestTime.Now), Ct);
        }
    }

    private async Task<IReadOnlyList<MemberTrackingDto>> MemberTrackingAsync()
    {
        await using var db = Fixture.CreateDbContext();
        return await CreateQuery(db).GetMemberTrackingAsync(Ct);
    }

    private async Task<EventCountsDto> QueryAsync(DateOnly from, DateOnly to)
    {
        await using var db = Fixture.CreateDbContext();
        return await CreateQuery(db).GetCountsAsync(from, to, Ct);
    }

    private DataOverviewQuery CreateQuery(Apollo.Data.ApolloDbContext db) =>
        new(db, [new FakeIdentityProvider()], new RoleSettingsQuery(db), _time);

    private static (DateOnly Day, int Count)[] Days(EventCountsDto counts, DataEventType type) =>
        [.. counts.Tables.Single(t => t.Type == type).Days.Select(d => (d.Day, d.Count))];

    private static DateTimeOffset Utc(int year, int month, int day, int hour) => new(year, month, day, hour, 0, 0, TimeSpan.Zero);
}
