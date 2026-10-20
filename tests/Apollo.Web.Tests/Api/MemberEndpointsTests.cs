using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Tests.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Tests.Api;

/// <summary>
/// The member stats API of the admin member page (#38) on <c>/api/admin/members/{userId}/stats</c>: the <c>Admin</c>
/// policy, unknown members, the tracking status and the member's stats with names resolved.
/// </summary>
public sealed class MemberEndpointsTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string AdminDiscordId = "80351110224678912";
    private const string Range = "from=2026-01-10&to=2026-01-15";

    private static readonly PlatformRef _admin = new(PlatformKind.Discord, AdminDiscordId);
    private static readonly PlatformRef _guild = ApolloWebApplicationFactory.Guild;

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetStats_WhenAnonymous_ShouldReturn401()
    {
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();

        using var response = await GetAsync(http, Path(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetStats_WhenUserIsNotAdministrator_ShouldReturn403()
    {
        var admin = await SeedAdminAsync(isAdministrator: false);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, Path(admin));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetStats_WhenUserIdUnknown_ShouldReturn404()
    {
        await SeedAdminAsync(isAdministrator: true);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, Path(Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStats_WhenUserIsNoMemberOfTheConfiguredGuild_ShouldReturn404()
    {
        await SeedAdminAsync(isAdministrator: true);
        var stranger = await SeedMemberAsync(TestPlatform.User("stranger"), "Fremd", guild: TestPlatform.Guild);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, Path(stranger));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStats_WhenUserIdIsNoGuid_ShouldReturn404()
    {
        await SeedAdminAsync(isAdministrator: true);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, $"/api/admin/members/80351110224678912/stats?{Range}");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetStats_WhenTheMemberHasAnOpenPeriod_ShouldBeTrackedSinceItsStart()
    {
        await SeedAdminAsync(isAdministrator: true);
        var anna = await SeedMemberAsync(TestPlatform.User("anna"), "Anna", avatarUrl: "https://cdn.example/anna.png");
        var guildId = await GuildIdAsync();
        await AddAsync(
            Period(guildId, anna, "2025-03-01T10:00:00Z", "2025-04-01T10:00:00Z"),
            Period(guildId, anna, "2026-01-05T23:30:00Z", null));
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var stats = await GetStatsAsync(http, anna);

        stats.Member.ShouldBe(new MemberProfileDto(anna, "Anna", "https://cdn.example/anna.png", "Discord"));
        stats.Tracking.ShouldBe(new TrackingStatusDto(TrackedSince: new DateOnly(2026, 1, 6), TrackedUntil: new DateOnly(2025, 4, 1)));
        stats.Tracking.IsTracked.ShouldBeTrue();
        stats.Activity.TrackedSince.ShouldBe(new DateOnly(2026, 1, 6));
    }

    [Fact]
    public async Task GetStats_WhenEveryPeriodIsClosed_ShouldBeNotTracked()
    {
        await SeedAdminAsync(isAdministrator: true);
        var anna = await SeedMemberAsync(TestPlatform.User("anna"), "Anna");
        await AddAsync(Period(await GuildIdAsync(), anna, "2026-01-05T10:00:00Z", "2026-01-12T10:00:00Z"));
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var stats = await GetStatsAsync(http, anna);

        stats.Tracking.ShouldBe(new TrackingStatusDto(TrackedSince: null, TrackedUntil: new DateOnly(2026, 1, 12)));
        (stats.Tracking.IsTracked, stats.Tracking.WasEverTracked).ShouldBe((false, true));
        stats.Activity.TrackedDays.ShouldBe(3);
    }

    [Fact]
    public async Task GetStats_WhenNeverTracked_ShouldSayNeverTrackedWithAnEmptyRange()
    {
        await SeedAdminAsync(isAdministrator: true);
        var anna = await SeedMemberAsync(TestPlatform.User("anna"), "Anna");
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var stats = await GetStatsAsync(http, anna);

        stats.Tracking.WasEverTracked.ShouldBeFalse();
        stats.Activity.Totals.IsEmpty.ShouldBeTrue();
        stats.Activity.Daily.Count.ShouldBe(6);
        stats.Highlights.TopEmojis.ShouldBeEmpty();
        stats.Highlights.VoiceBuddies.ShouldBeEmpty();
        stats.Highlights.LongestVoiceSession.ShouldBeNull();
        stats.Highlights.TopGames.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetStats_ShouldReturnTheMembersActivityAndHighlightsWithNames()
    {
        await SeedAdminAsync(isAdministrator: true);
        var anna = await SeedMemberAsync(TestPlatform.User("anna"), "Anna");
        var ben = await SeedMemberAsync(TestPlatform.User("ben"), "Ben");
        var guildId = await GuildIdAsync();
        var general = await SeedChannelAsync(TestPlatform.Channel("general"), "allgemein");
        var lounge = await SeedChannelAsync(TestPlatform.Channel("lounge"), "Voice-Lounge");
        await AddAsync(
            Period(guildId, anna, "2026-01-01T00:00:00Z", null),
            new MessageEventBuilder().In(guildId, general).By(anna).At("2026-01-12T10:00:00Z").ReplyTo(ben).Build(),
            new MessageEventBuilder().In(guildId, general).By(ben).At("2026-01-12T10:05:00Z").ReplyTo(anna).Build(),
            new MessageEventBuilder().In(guildId, general).By(ben).At("2026-01-12T10:06:00Z").ReplyTo(anna).Build(),
            new ReactionEventBuilder().In(guildId, general).By(anna).WithEmoji("👍").At("2026-01-12T11:00:00Z").Build(),
            new ReactionEventBuilder().In(guildId, general).By(anna).WithCustomEmoji("900000000000000001", "apollo").At("2026-01-12T11:01:00Z").Build(),
            new VoiceSessionBuilder().In(guildId, lounge).By(anna).From("2026-01-13T18:00:00Z").Until("2026-01-13T20:00:00Z").Build(),
            new VoiceSessionBuilder().In(guildId, lounge).By(ben).From("2026-01-13T19:00:00Z").Until("2026-01-13T21:00:00Z").Build());
        await AddAsync(new PresenceActivitySessionBuilder().In(guildId).By(anna).Playing("Factorio").From("2026-01-14T18:00:00Z").Until("2026-01-14T19:30:00Z").BuildRows().All);
        await Fixture.RefreshContinuousAggregatesAsync(Ct);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var stats = await GetStatsAsync(http, anna);

        stats.Activity.Totals.ShouldBe(new ActivityCountsDto(Messages: 1, Characters: stats.Activity.Totals.Characters, ReactionsGiven: 2, ReactionsReceived: 0, VoiceSeconds: 7200));
        stats.Activity.Channels.Select(c => c.Name).ShouldBe(["Voice-Lounge", "allgemein"], ignoreOrder: true);
        // Tuesday 13 Jan 19:00 to 21:00 CET.
        (stats.Activity.VoiceHeatmap[1][19], stats.Activity.VoiceHeatmap[1][20]).ShouldBe((3600L, 3600L));
        var highlights = stats.Highlights;
        highlights.TopEmojis.ShouldBe([new EmojiUsageDto("apollo", IsCustom: true, 1), new EmojiUsageDto("👍", IsCustom: false, 1)], ignoreOrder: true);
        highlights.VoiceBuddies.ShouldBe([new VoiceBuddyDto(new EventUserDto(ben, "Ben"), 3600)]);
        highlights.RepliesTo.ShouldBe([new ReplyPartnerDto(new EventUserDto(ben, "Ben"), 1)]);
        highlights.RepliedBy.ShouldBe([new ReplyPartnerDto(new EventUserDto(ben, "Ben"), 2)]);
        highlights.LongestVoiceSession.ShouldBe(new LongestVoiceSessionDto(new EventChannelDto(lounge, "Voice-Lounge"), Utc("2026-01-13T18:00:00Z"), 7200));
        highlights.TopGames.ShouldBe([new GamePlaytimeDto("Factorio", 5400)]);
    }

    [Theory]
    [InlineData("from=2026-01-15&to=2026-01-14", "from")]
    [InlineData("from=2026-01-15", "to")]
    [InlineData("from=1999-12-31&to=2026-01-15", "from")]
    [InlineData("from=2026-01-01&to=2026-01-17", "to")]
    public async Task GetStats_WhenRangeInvalid_ShouldReturn400(string query, string field)
    {
        var admin = await SeedAdminAsync(isAdministrator: true);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, $"/api/admin/members/{admin}/stats?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct)).ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe([field]);
    }

    private ApolloWebApplicationFactory CreateFactory() =>
        new() { ConnectionString = Fixture.ConnectionString, TimeProvider = _time };

    private static string Path(Guid userId) => $"/api/admin/members/{userId}/stats?{Range}";

    private async Task<Guid> SeedAdminAsync(bool isAdministrator)
    {
        await using var db = Fixture.CreateDbContext();
        var ids = Ids(db);
        await new GuildBuilder().For(_guild).Named("Apollo").SeedAsync(db, ids, Ct);
        var user = await new UserBuilder().For(_admin).Named("Nelly").SeedAsync(db, ids, Ct);
        await new GuildMemberBuilder().For(_admin, _guild).AsAdministrator(isAdministrator).SeedAsync(db, ids, Ct);
        return user.Id;
    }

    private async Task<Guid> SeedMemberAsync(PlatformRef user, string name, string? avatarUrl = null, PlatformRef? guild = null)
    {
        await using var db = Fixture.CreateDbContext();
        var ids = Ids(db);
        var seeded = await new UserBuilder().For(user).Named(name).WithAvatar(avatarUrl).SeedAsync(db, ids, Ct);
        await new GuildMemberBuilder().For(user, guild ?? _guild).SeedAsync(db, ids, Ct);
        return seeded.Id;
    }

    private async Task<Guid> SeedChannelAsync(PlatformRef channel, string name)
    {
        await using var db = Fixture.CreateDbContext();
        return (await new ChannelBuilder().For(channel, _guild).Named(name).SeedAsync(db, Ids(db), Ct)).Id;
    }

    private async Task<Guid> GuildIdAsync()
    {
        await using var db = Fixture.CreateDbContext();
        return await Ids(db).ResolveGuildAsync(_guild, Ct);
    }

    private static TrackingPeriod Period(Guid guildId, Guid userId, string fromUtc, string? toUtc) => new()
    {
        GuildId = guildId,
        UserId = userId,
        From = Utc(fromUtc),
        To = toUtc is null ? null : Utc(toUtc),
    };

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);

    private async Task AddAsync(params object[] rows)
    {
        await using var db = Fixture.CreateDbContext();
        db.AddRange(rows);
        await db.SaveChangesAsync(Ct);
    }

    private static PlatformIdResolver Ids(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);

    /// <summary>Signs in through <c>SignInCompletion</c> with a browser-session cookie, so the test clock cannot expire it.</summary>
    private static async Task SignInAsync(HttpClient http)
    {
        using var response = await http.GetAsync(new Uri($"{TestSignInEndpoints.PlatformSignInPath}?id={AdminDiscordId}", UriKind.Relative), Ct);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<MemberStatsDto> GetStatsAsync(HttpClient http, Guid userId)
    {
        using var response = await GetAsync(http, Path(userId));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<MemberStatsDto>(Ct)).ShouldNotBeNull();
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient http, string path) => http.GetAsync(new Uri(path, UriKind.Relative), Ct);
}
