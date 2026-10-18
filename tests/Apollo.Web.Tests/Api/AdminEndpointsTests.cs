using System.Net;
using System.Net.Http.Json;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Status;
using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Tests.Auth;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Tests.Api;

/// <summary>
/// The <c>Admin</c> policy on <c>/api/admin</c> (#30) against seeded guild state: the Administrator flag the bot syncs
/// into <c>guild_members</c> (#13) is the only way in, and the access check repeats every revalidation interval.
/// </summary>
public sealed class AdminEndpointsTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string BotStatusPath = "/api/admin/bot-status";
    private const string DiscordUserId = "80351110224678912";

    private static readonly TimeSpan _interval = TimeSpan.FromMinutes(5);
    private static readonly PlatformRef _discordUser = new(PlatformKind.Discord, DiscordUserId);

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetBotStatus_WhenAdmin_ShouldReturn200()
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory(useTestClock: false);
        using var http = factory.CreateHttpsClient();
        await LoginAsync(http);

        using var response = await GetAsync(http, BotStatusPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var status = (await response.Content.ReadFromJsonAsync<BotStatusDto>(Ct)).ShouldNotBeNull();
        status.HasHeartbeat.ShouldBeFalse();
        status.EventsPerHour.Count.ShouldBe(24);
    }

    [Fact]
    public async Task GetBotStatus_WhenAdminAndHeartbeatRecent_ShouldReturnTheHeartbeat()
    {
        await SeedMemberAsync(isAdministrator: true);
        await using (var db = Fixture.CreateDbContext())
        {
            db.BotStatuses.Add(new BotStatus
            {
                GuildId = await Ids(db).ResolveGuildAsync(ApolloWebApplicationFactory.Guild, Ct),
                OccurredAt = TestTime.Now - TimeSpan.FromSeconds(30),
                Connected = true,
                GatewayLatencyMs = 84,
                LastEventAt = TestTime.Now - TimeSpan.FromSeconds(12),
                OpenVoiceSessions = 3,
            });
            await db.SaveChangesAsync(Ct);
        }

        await using var factory = CreateFactory(useTestClock: true);
        using var http = factory.CreateHttpsClient();
        await PlatformSignInAsync(http);

        var status = (await http.GetFromJsonAsync<BotStatusDto>(new Uri(BotStatusPath, UriKind.Relative), Ct)).ShouldNotBeNull();

        status.ShouldBe(new BotStatusDto(
            TestTime.Now,
            TestTime.Now - TimeSpan.FromSeconds(30),
            Connected: true,
            LastConnectedAt: TestTime.Now - TimeSpan.FromSeconds(30),
            GatewayLatencyMeanMs: 84,
            TestTime.Now - TimeSpan.FromSeconds(12),
            LastRecordedEvent: null,
            status.OpenVoiceSessions,
            IsStale: false,
            NoTrackingRole: false,
            status.EventsPerHour));
        status.OpenVoiceSessions.ShouldBeEmpty();
    }

    [Fact]
    public async Task AdminSession_WhenCalled_ShouldBeGone()
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory(useTestClock: false);
        using var http = factory.CreateHttpsClient();
        await LoginAsync(http);

        using var response = await GetAsync(http, "/api/admin/session");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AdminPage_WhenUserIsAdministrator_ShouldServeTheApp()
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory(useTestClock: false);
        using var http = factory.CreateHttpsClient();
        await LoginAsync(http);

        using var response = await GetAsync(http, "/admin/members");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
    }

    [Fact]
    public async Task GetBotStatus_WhenNotAdmin_ShouldReturn403()
    {
        await SeedMemberAsync(isAdministrator: false);
        await using var factory = CreateFactory(useTestClock: false);
        using var http = factory.CreateHttpsClient();
        await LoginAsync(http);

        using var response = await GetAsync(http, BotStatusPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task GetBotStatus_WhenAnonymous_ShouldReturn401()
    {
        await using var factory = CreateFactory(useTestClock: false);
        using var http = factory.CreateHttpsClient();

        using var response = await GetAsync(http, BotStatusPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task AdminApi_WhenUserIsNotGuildMember_ShouldReturn403()
    {
        await using (var db = Fixture.CreateDbContext())
        {
            await new GuildMemberBuilder().For(_discordUser, new PlatformRef(PlatformKind.Discord, "333333333333333333")).AsAdministrator()
                .SeedAsync(db, Ids(db), Ct);
        }

        await using var factory = CreateFactory(useTestClock: false);
        using var http = factory.CreateHttpsClient();
        await LoginAsync(http);

        using var response = await GetAsync(http, BotStatusPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Theory]
    [InlineData(RoleSettingPurpose.MemberAccess)]
    [InlineData(RoleSettingPurpose.Tracking)]
    public async Task AdminApi_WhenUserHoldsAConfiguredRoleWithoutAdministratorFlag_ShouldReturn403(RoleSettingPurpose purpose)
    {
        await using (var db = Fixture.CreateDbContext())
        {
            var role = new PlatformRef(PlatformKind.Discord, "444444444444444444");
            var member = await new GuildMemberBuilder().For(_discordUser, ApolloWebApplicationFactory.Guild).SeedAsync(db, Ids(db), Ct);
            var roleRow = await new RoleBuilder().For(role, ApolloWebApplicationFactory.Guild).SeedAsync(db, Ids(db), Ct);
            db.MemberRoles.Add(new() { GuildId = member.GuildId, UserId = member.UserId, RoleId = roleRow.Id });
            await db.SaveChangesAsync(Ct);
            await new RoleSettingBuilder().ForRole(role, ApolloWebApplicationFactory.Guild).WithPurpose(purpose).SeedAsync(db, Ids(db), Ct);
        }

        await using var factory = CreateFactory(useTestClock: false);
        using var http = factory.CreateHttpsClient();
        await LoginAsync(http);

        using var response = await GetAsync(http, BotStatusPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminApi_WhenAdministratorRevokedAndIntervalElapsed_ShouldReturn403()
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory(useTestClock: true);
        using var http = factory.CreateHttpsClient();
        await PlatformSignInAsync(http);
        (await GetAsync(http, BotStatusPath)).StatusCode.ShouldBe(HttpStatusCode.OK);
        await SetAdministratorAsync(false);
        _time.Advance(_interval);

        using var response = await GetAsync(http, BotStatusPath);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AdminApi_WhenAdministratorRevokedAndIntervalNotElapsed_ShouldStillUseTheClaims()
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory(useTestClock: true);
        using var http = factory.CreateHttpsClient();
        await PlatformSignInAsync(http);
        await SetAdministratorAsync(false);
        _time.Advance(_interval - TimeSpan.FromSeconds(1));

        using var response = await GetAsync(http, BotStatusPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AdminApi_WhenAdministratorGrantedAndIntervalElapsed_ShouldReturn200WithoutNewLogin()
    {
        await SeedMemberAsync(isAdministrator: false);
        await using var factory = CreateFactory(useTestClock: true);
        using var http = factory.CreateHttpsClient();
        await PlatformSignInAsync(http);
        (await GetAsync(http, BotStatusPath)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await SetAdministratorAsync(true);
        _time.Advance(_interval);

        using var response = await GetAsync(http, BotStatusPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var me = await http.GetFromJsonAsync<CurrentUserDto>(new Uri("/auth/me", UriKind.Relative), Ct);
        me.ShouldNotBeNull().IsAdmin.ShouldBeTrue();
    }

    [Fact]
    public async Task AdminApi_WhenCookieCopiedBeforeLogoutAndIntervalElapsed_ShouldReturn401()
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory(useTestClock: true);
        using var http = factory.CreateHttpsClient();
        var copiedCookie = await PlatformSignInAsync(http);
        _time.Advance(TimeSpan.FromSeconds(1));
        await LogoutAsync(http);
        _time.Advance(_interval);
        using var thief = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = ApolloWebApplicationFactory.BaseAddress,
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, BotStatusPath);
        request.Headers.Add("Cookie", copiedCookie);

        using var response = await thief.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.GetValues("Set-Cookie").ShouldContain(c => c.StartsWith(".Apollo.Session=;", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Login_AfterLogout_ShouldStartAWorkingSession()
    {
        await SeedMemberAsync(isAdministrator: true);
        await using var factory = CreateFactory(useTestClock: true);
        using var http = factory.CreateHttpsClient();
        await PlatformSignInAsync(http);
        await LogoutAsync(http);
        _time.Advance(TimeSpan.FromSeconds(1));
        await PlatformSignInAsync(http);
        _time.Advance(_interval);

        using var response = await GetAsync(http, BotStatusPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private ApolloWebApplicationFactory CreateFactory(bool useTestClock) =>
        new() { ConnectionString = Fixture.ConnectionString, TimeProvider = useTestClock ? _time : null };

    private async Task<Guid> SeedMemberAsync(bool isAdministrator)
    {
        await using var db = Fixture.CreateDbContext();
        var member = await new GuildMemberBuilder().For(_discordUser, ApolloWebApplicationFactory.Guild).AsAdministrator(isAdministrator)
            .SeedAsync(db, Ids(db), Ct);
        return member.UserId;
    }

    private async Task SetAdministratorAsync(bool isAdministrator)
    {
        await using var db = Fixture.CreateDbContext();
        await db.GuildMembers.ExecuteUpdateAsync(s => s.SetProperty(m => m.IsAdministrator, isAdministrator), Ct);
    }

    // The real login: platform callback, then /auth/complete (persistent cookie, so only with the system clock).
    private static async Task LoginAsync(HttpClient http)
    {
        (await GetAsync(http, $"{TestSignInEndpoints.ExternalSignInPath}?id={DiscordUserId}&name=Nelly")).EnsureSuccessStatusCode();
        (await GetAsync(http, "/auth/complete")).StatusCode.ShouldBe(HttpStatusCode.Redirect);
    }

    /// <summary>Signs in through <c>SignInCompletion</c> with a browser-session cookie; returns the cookie as a <c>Cookie</c> header.</summary>
    private static async Task<string> PlatformSignInAsync(HttpClient http)
    {
        using var response = await GetAsync(http, $"{TestSignInEndpoints.PlatformSignInPath}?id={DiscordUserId}");
        response.EnsureSuccessStatusCode();
        var setCookie = response.Headers.GetValues("Set-Cookie").Single(c => c.StartsWith(".Apollo.Session=", StringComparison.Ordinal));
        return setCookie.Split(';')[0];
    }

    private static async Task LogoutAsync(HttpClient http)
    {
        var antiforgery = await http.GetFromJsonAsync<AntiforgeryTokenDto>(new Uri("/auth/antiforgery", UriKind.Relative), Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add(antiforgery!.HeaderName, antiforgery.RequestToken);
        using var response = await http.SendAsync(request, Ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient http, string path) => http.GetAsync(new Uri(path, UriKind.Relative), Ct);

    private static PlatformIdResolver Ids(ApolloDbContext db) => new(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
}
