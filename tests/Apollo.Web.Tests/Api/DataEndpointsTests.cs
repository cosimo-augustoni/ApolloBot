using System.Net;
using System.Net.Http.Json;
using Apollo.Data.Identity;
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
/// The data API of the admin page (#32) on <c>/api/admin/data</c>: the <c>Admin</c> policy, defaults and validation.
/// </summary>
public sealed class DataEndpointsTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string DiscordUserId = "80351110224678912";

    private static readonly PlatformRef _discordUser = new(PlatformKind.Discord, DiscordUserId);

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string> Endpoints =>
    [
        "/api/admin/data/counts",
        "/api/admin/data/members",
        "/api/admin/data/events",
        "/api/admin/data/users?search=a",
        "/api/admin/data/channels?search=a",
    ];

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WhenAnonymous_ShouldReturn401(string path)
    {
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();

        using var response = await GetAsync(http, path);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WhenUserIsNotAdministrator_ShouldReturn403(string path)
    {
        await SeedMemberAsync(isAdministrator: false);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, path);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WhenAdmin_ShouldReturn200(string path)
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetCounts_WhenNoRange_ShouldCountTheLast30Days()
    {
        await SeedMemberAsync(isAdministrator: true);
        await new EventSeeder(Fixture, Ct).AddAsync(DataEventType.Message, TestTime.Now.AddDays(-29));
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var counts = (await http.GetFromJsonAsync<EventCountsDto>(new Uri("/api/admin/data/counts", UriKind.Relative), Ct)).ShouldNotBeNull();

        counts.From.ShouldBe(new DateOnly(2025, 12, 17));
        counts.To.ShouldBe(new DateOnly(2026, 1, 15));
        counts.Tables.Count.ShouldBe(6);
        counts.Tables[0].Days.Count.ShouldBe(30);
        counts.Tables[0].Total.ShouldBe(1);
    }

    [Theory]
    [InlineData("from=2026-01-15&to=2026-01-14", "from")]
    [InlineData("from=2025-01-14&to=2026-01-15", "to")]
    public async Task GetCounts_WhenRangeInvalid_ShouldReturn400(string query, string field)
    {
        var problem = await GetProblemAsync($"/api/admin/data/counts?{query}");

        problem.Errors.Keys.ShouldBe([field]);
    }

    [Fact]
    public async Task GetMembers_ShouldReportTheModeAsCamelCase()
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var json = await http.GetStringAsync(new Uri("/api/admin/data/members", UriKind.Relative), Ct);

        json.ShouldBe("""[{"platform":"Discord","tracked":0,"untracked":1,"total":1,"mode":"noTrackingRole"}]""");
    }

    [Fact]
    public async Task GetEvents_WhenNoFilter_ShouldReturnMessagesOfTheLast24Hours()
    {
        await SeedMemberAsync(isAdministrator: true);
        var events = new EventSeeder(Fixture, Ct);
        var recent = await events.AddAsync(DataEventType.Message, TestTime.Now.AddHours(-23));
        await events.AddAsync(DataEventType.Message, TestTime.Now.AddHours(-25));
        await events.AddAsync(DataEventType.Reaction, TestTime.Now.AddHours(-1));
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var page = (await http.GetFromJsonAsync<RawEventPageDto>(new Uri("/api/admin/data/events", UriKind.Relative), Ct)).ShouldNotBeNull();

        page.Total.ShouldBe(1);
        page.Items.ShouldHaveSingleItem().Id.ShouldBe(recent);
    }

    [Fact]
    public async Task GetEvents_WhenTypeGiven_ShouldBrowseThatTable()
    {
        await SeedMemberAsync(isAdministrator: true);
        await new EventSeeder(Fixture, Ct).AddAsync(DataEventType.VoiceSession, TestTime.Now.AddHours(-1));
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var page = (await http.GetFromJsonAsync<RawEventPageDto>(
            new Uri("/api/admin/data/events?type=voiceSession&page=1&pageSize=200", UriKind.Relative), Ct)).ShouldNotBeNull();

        page.Items.ShouldHaveSingleItem().Type.ShouldBe(DataEventType.VoiceSession);
    }

    [Fact]
    public async Task GetEvents_WhenPageSizeTooLarge_ShouldReturn400()
    {
        var problem = await GetProblemAsync("/api/admin/data/events?pageSize=201");

        problem.Errors.Keys.ShouldBe(["pageSize"]);
    }

    [Theory]
    [InlineData("type=chat")]
    [InlineData("type=0")]
    [InlineData("type=")]
    public async Task GetEvents_WhenTypeInvalid_ShouldReturn400(string query)
    {
        var problem = await GetProblemAsync($"/api/admin/data/events?{query}");

        problem.Errors.Keys.ShouldBe(["type"]);
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("from=2026-01-15T10:00:00Z&to=2026-01-15T09:00:00Z", "from")]
    [InlineData("from=2025-01-14T00:00:00Z&to=2026-01-15T00:00:01Z", "to")]
    public async Task GetEvents_WhenFilterInvalid_ShouldReturn400(string query, string field)
    {
        var problem = await GetProblemAsync($"/api/admin/data/events?{query}");

        problem.Errors.Keys.ShouldBe([field]);
    }

    [Fact]
    public async Task GetEvents_ShouldNotContainMessageContent()
    {
        await SeedMemberAsync(isAdministrator: true);
        await new EventSeeder(Fixture, Ct).AddAsync(DataEventType.Message, TestTime.Now.AddHours(-1));
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var json = await http.GetStringAsync(new Uri("/api/admin/data/events", UriKind.Relative), Ct);

        json.ShouldContain("\"message\":{\"kind\":\"Create\"");
        json.ShouldNotContain("content", Case.Insensitive);
    }

    private async Task<HttpValidationProblemDetails> GetProblemAsync(string path)
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await GetAsync(http, path);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("application/problem+json");
        return (await response.Content.ReadFromJsonAsync<HttpValidationProblemDetails>(Ct)).ShouldNotBeNull();
    }

    private ApolloWebApplicationFactory CreateFactory() =>
        new() { ConnectionString = Fixture.ConnectionString, TimeProvider = _time };

    private async Task SeedMemberAsync(bool isAdministrator)
    {
        await using var db = Fixture.CreateDbContext();
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        await new GuildBuilder().For(ApolloWebApplicationFactory.Guild).Named("Apollo").SeedAsync(db, ids, Ct);
        await new UserBuilder().For(_discordUser).Named("Nelly").SeedAsync(db, ids, Ct);
        await new GuildMemberBuilder().For(_discordUser, ApolloWebApplicationFactory.Guild).AsAdministrator(isAdministrator).SeedAsync(db, ids, Ct);
    }

    /// <summary>Signs in through <c>SignInCompletion</c> with a browser-session cookie, so the test clock cannot expire it.</summary>
    private static async Task SignInAsync(HttpClient http)
    {
        using var response = await http.GetAsync(new Uri($"{TestSignInEndpoints.PlatformSignInPath}?id={DiscordUserId}", UriKind.Relative), Ct);
        response.EnsureSuccessStatusCode();
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient http, string path) => http.GetAsync(new Uri(path, UriKind.Relative), Ct);
}
