using System.Net;
using System.Net.Http.Json;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Tests.Admin;
using Apollo.Web.Tests.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Tests.Api;

/// <summary>
/// The server stats API of the admin dashboard (#37) on <c>/api/admin/stats/server</c>: the <c>Admin</c> policy, the
/// range validation and the stats of the configured guild.
/// </summary>
public sealed class StatsEndpointsTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string DiscordUserId = "80351110224678912";
    private const string ServerPath = "/api/admin/stats/server?from=2026-01-10&to=2026-01-15";

    private static readonly PlatformRef _discordUser = new(PlatformKind.Discord, DiscordUserId);

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetServer_WhenAnonymous_ShouldReturn401()
    {
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();

        using var response = await GetAsync(http, ServerPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetServer_WhenUserIsNotAdministrator_ShouldReturn403()
    {
        await SeedMemberAsync(isAdministrator: false);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, ServerPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetServer_WhenAdmin_ShouldReturnTheStatsOfTheConfiguredGuildWithoutBots()
    {
        await SeedMemberAsync(isAdministrator: true);
        var events = new EventSeeder(Fixture, Ct);
        var general = await events.ChannelAsync(EventSeeder.General, "allgemein");
        await events.AddAsync(DataEventType.Message, TestTime.Now.AddDays(-1));
        await events.AddAsync(DataEventType.Message, TestTime.Now.AddHours(-1));
        await events.AddAsync(DataEventType.Message, TestTime.Now.AddHours(-1), guild: TestPlatform.Guild);
        await events.AddAsync(DataEventType.Message, TestTime.Now.AddHours(-1), userId: await SeedBotAsync());
        await Fixture.RefreshContinuousAggregatesAsync(Ct);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var stats = (await http.GetFromJsonAsync<ActivityStatsDto>(new Uri(ServerPath, UriKind.Relative), Ct)).ShouldNotBeNull();

        (stats.From, stats.To).ShouldBe((new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15)));
        stats.Totals.ShouldBe(new ActivityCountsDto(Messages: 2, Characters: 84, ReactionsGiven: 0, ReactionsReceived: 0, VoiceSeconds: 0));
        (stats.ActiveDays, stats.ActiveUsers, stats.TrackedDays, stats.TrackedSince).ShouldBe((2, (int?)1, (int?)null, (DateOnly?)null));
        stats.Daily.Count.ShouldBe(6);
        stats.Daily[^1].ShouldBe(new DailyActivityDto(new DateOnly(2026, 1, 15), new ActivityCountsDto(1, 42, 0, 0, 0)));
        stats.Channels.ShouldHaveSingleItem().ShouldBe(new ChannelActivityDto(general, "allgemein", IsDeleted: false, Messages: 2, VoiceSeconds: 0));
        stats.Heatmap.Count.ShouldBe(7);
        stats.Heatmap.Sum(day => day.Sum()).ShouldBe(2);
    }

    [Fact]
    public async Task GetServer_WhenTheBotNeverSawTheGuild_ShouldReturnAnEmptyRange()
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);
        await DeleteGuildMappingAsync();

        var stats = (await http.GetFromJsonAsync<ActivityStatsDto>(new Uri(ServerPath, UriKind.Relative), Ct)).ShouldNotBeNull();

        stats.Totals.IsEmpty.ShouldBeTrue();
        stats.Daily.Count.ShouldBe(6);
        stats.Channels.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("from=2026-01-15&to=2026-01-14", "from")]
    [InlineData("from=2026-13-01&to=2026-01-14", "from")]
    [InlineData("from=15.01.2026&to=2026-01-15", "from")]
    [InlineData("from=2026-01-15&to=morgen", "to")]
    [InlineData("to=2026-01-15", "from")]
    [InlineData("from=2026-01-15", "to")]
    public async Task GetServer_WhenRangeInvalid_ShouldReturn400(string query, string field)
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, $"/api/admin/stats/server?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        var problem = (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct)).ShouldNotBeNull();
        problem.Errors.Keys.ShouldBe([field]);
    }

    private ApolloWebApplicationFactory CreateFactory() =>
        new() { ConnectionString = Fixture.ConnectionString, TimeProvider = _time };

    private async Task SeedMemberAsync(bool isAdministrator)
    {
        await using var db = Fixture.CreateDbContext();
        var ids = Ids(db);
        await new GuildBuilder().For(ApolloWebApplicationFactory.Guild).Named("Apollo").SeedAsync(db, ids, Ct);
        await new UserBuilder().For(_discordUser).Named("Nelly").SeedAsync(db, ids, Ct);
        await new GuildMemberBuilder().For(_discordUser, ApolloWebApplicationFactory.Guild).AsAdministrator(isAdministrator).SeedAsync(db, ids, Ct);
    }

    private async Task<Guid> SeedBotAsync()
    {
        var bot = TestPlatform.User("bot");
        await using var db = Fixture.CreateDbContext();
        var ids = Ids(db);
        var user = await new UserBuilder().For(bot).Named("Robo").SeedAsync(db, ids, Ct);
        await new GuildMemberBuilder().For(bot, ApolloWebApplicationFactory.Guild).AsBot().SeedAsync(db, ids, Ct);
        return user.Id;
    }

    /// <summary>The admin's member row stays (access needs it), but the guild's platform mapping goes, as before the bot's first sync.</summary>
    private async Task DeleteGuildMappingAsync()
    {
        await using var db = Fixture.CreateDbContext();
        db.GuildPlatformIds.RemoveRange(db.GuildPlatformIds);
        await db.SaveChangesAsync(Ct);
    }

    private static PlatformIdResolver Ids(Apollo.Data.ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);

    /// <summary>Signs in through <c>SignInCompletion</c> with a browser-session cookie, so the test clock cannot expire it.</summary>
    private static async Task SignInAsync(HttpClient http)
    {
        using var response = await http.GetAsync(new Uri($"{TestSignInEndpoints.PlatformSignInPath}?id={DiscordUserId}", UriKind.Relative), Ct);
        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient http, string path) => http.GetAsync(new Uri(path, UriKind.Relative), Ct);
}
