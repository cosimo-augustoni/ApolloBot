using System.Net;
using System.Net.Http.Json;
using Apollo.Data.Identity;
using Apollo.Data.Status;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Tests.Admin;
using Apollo.Web.Tests.Auth;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Tests.Api;

/// <summary>
/// The data sanity API (#40) on <c>/api/admin/sanity</c>: the <c>Admin</c> policy, the range (default the last 30 Zurich
/// days) and the report of the configured guild with the users named.
/// </summary>
public sealed class SanityEndpointsTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string DiscordUserId = "80351110224678912";
    private const string SanityPath = "/api/admin/sanity?from=2026-01-10&to=2026-01-15";

    private static readonly PlatformRef _discordUser = new(PlatformKind.Discord, DiscordUserId);

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetSanity_WhenAnonymous_ShouldReturn401()
    {
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();

        using var response = await GetAsync(http, SanityPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetSanity_WhenUserIsNotAdministrator_ShouldReturn403()
    {
        await SeedMemberAsync(isAdministrator: false);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, SanityPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetSanity_WhenAdmin_ShouldReturnTheReportOfTheConfiguredGuildWithTheUsersNamed()
    {
        await SeedMemberAsync(isAdministrator: true);
        var events = new EventSeeder(Fixture, Ct);
        var alice = await events.UserAsync(EventSeeder.Alice, "Alice");
        await events.ChannelAsync(EventSeeder.General, "allgemein");
        var untracked = await events.AddAsync(DataEventType.Message, TestTime.Now.AddHours(-1));
        await events.AddAsync(DataEventType.Message, TestTime.Now.AddHours(-1), guild: Apollo.Testing.Platform.TestPlatform.Guild);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var report = (await http.GetFromJsonAsync<DataSanityDto>(new Uri(SanityPath, UriKind.Relative), Ct)).ShouldNotBeNull();

        (report.From, report.To).ShouldBe((new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 15)));
        report.UntrackedEvents.Count.ShouldBe(1);
        report.UntrackedEvents.Examples.ShouldBe(
            [new UntrackedRowDto("message_events", "user_id", untracked, new EventUserDto(alice, "Alice"), TestTime.Now.AddHours(-1))]);
        report.UntrackedReferences.Count.ShouldBe(0);
        report.HeartbeatGapThresholdSeconds.ShouldBe(120);
    }

    [Fact]
    public async Task GetSanity_WithoutARange_ShouldCheckTheLast30ZurichDaysUpToToday()
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var report = (await http.GetFromJsonAsync<DataSanityDto>(new Uri("/api/admin/sanity", UriKind.Relative), Ct)).ShouldNotBeNull();

        (report.From, report.To).ShouldBe((new DateOnly(2025, 12, 17), new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public async Task GetSanity_ShouldUseTheStaleThresholdOfTheBotStatusPageForGaps()
    {
        await SeedMemberAsync(isAdministrator: true);
        await SeedHeartbeatsAsync(TestTime.Now.AddHours(-2), TestTime.Now.AddHours(-2).AddMinutes(4));
        await using var factory = CreateFactory();
        await using var lenient = factory.WithWebHostBuilder(b => b.UseSetting("Apollo:Web:BotStatus:StaleAfter", "00:05:00"));
        using var http = lenient.CreateClient(new() { BaseAddress = ApolloWebApplicationFactory.BaseAddress, AllowAutoRedirect = false });
        await SignInAsync(http);

        var report = (await http.GetFromJsonAsync<DataSanityDto>(new Uri(SanityPath, UriKind.Relative), Ct)).ShouldNotBeNull();

        report.HeartbeatGapThresholdSeconds.ShouldBe(300);
        report.HeartbeatGaps.Count.ShouldBe(0);
    }

    [Fact]
    public async Task GetSanity_ShouldReportHeartbeatGaps()
    {
        await SeedMemberAsync(isAdministrator: true);
        var start = TestTime.Now.AddHours(-2);
        await SeedHeartbeatsAsync(start, start.AddMinutes(4));
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var report = (await http.GetFromJsonAsync<DataSanityDto>(new Uri(SanityPath, UriKind.Relative), Ct)).ShouldNotBeNull();

        report.HeartbeatGaps.Examples.ShouldBe([new HeartbeatGapDto(start, start.AddMinutes(4))]);
    }

    [Theory]
    [InlineData("from=2026-01-15&to=2026-01-14", "from")]
    [InlineData("from=2026-01-15", "to")]
    [InlineData("to=2026-01-15", "from")]
    [InlineData("from=2026-01-01&to=2026-01-17", "to")]
    public async Task GetSanity_WhenRangeInvalid_ShouldReturn400(string query, string field)
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, $"/api/admin/sanity?{query}");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
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

    private async Task SeedHeartbeatsAsync(params DateTimeOffset[] at)
    {
        await using var db = Fixture.CreateDbContext();
        var guildId = await Ids(db).ResolveGuildAsync(ApolloWebApplicationFactory.Guild, Ct);
        db.BotStatuses.AddRange(at.Select(a => new BotStatus { GuildId = guildId, OccurredAt = a, Connected = true }));
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
