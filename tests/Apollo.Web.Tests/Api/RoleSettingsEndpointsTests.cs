using System.Net;
using System.Net.Http.Json;
using Apollo.Data;
using Apollo.Data.Guilds;
using Apollo.Data.Identity;
using Apollo.Data.Tracking;
using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Web.Client.Contracts;
using Apollo.Web.Tests.Auth;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Tests.Api;

/// <summary>
/// The role settings API of the admin page (#33) on <c>/api/admin/settings</c>, against seeded guild state: the
/// <c>Admin</c> policy, the configured guild per platform, validation, antiforgery and the audit author.
/// </summary>
public sealed class RoleSettingsEndpointsTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string DiscordUserId = "80351110224678912";
    private const string SettingsPath = "/api/admin/settings/Discord";

    private static readonly PlatformRef _discordUser = new(PlatformKind.Discord, DiscordUserId);
    private static readonly PlatformRef _statistik = new(PlatformKind.Discord, "500000000000000001");
    private static readonly PlatformRef _mitglied = new(PlatformKind.Discord, "500000000000000002");
    private static readonly PlatformRef _gone = new(PlatformKind.Discord, "500000000000000003");

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static TheoryData<string, string> Endpoints => new()
    {
        { "GET", "/api/admin/settings/platforms" },
        { "GET", SettingsPath },
        { "GET", $"{SettingsPath}/preview?roleIds={Guid.Empty}" },
        { "GET", $"{SettingsPath}/history?take=20" },
        { "PUT", $"{SettingsPath}/tracking-role" },
        { "PUT", $"{SettingsPath}/member-access-roles" },
    };

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WhenAnonymous_ShouldReturn401(string method, string path)
    {
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();

        using var response = await SendAsync(http, method, path);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Theory]
    [MemberData(nameof(Endpoints))]
    public async Task Endpoint_WhenUserIsNotAdministrator_ShouldReturn403(string method, string path)
    {
        await SeedAsync(isAdministrator: false);
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await SendAsync(http, method, path);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetPlatforms_ShouldListThePlatformsWithASyncedGuild()
    {
        await SeedAsync();
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var platforms = await http.GetFromJsonAsync<string[]>(new Uri("/api/admin/settings/platforms", UriKind.Relative), Ct);

        platforms.ShouldBe(["Discord"]);
    }

    [Fact]
    public async Task GetSettings_WhenNoTrackingRoleIsSet_ShouldReportNoTrackingRoleWithRolesAndBotCheck()
    {
        var seed = await SeedAsync();
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await SendAsync(http, "GET", SettingsPath);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"mode\":\"noTrackingRole\"");
        var settings = (await response.Content.ReadFromJsonAsync<RoleSettingsDto>(Ct)).ShouldNotBeNull();
        settings.Platform.ShouldBe("Discord");
        settings.Roles.ShouldBe(
        [
            new SettingsRoleDto(seed.Statistik, "Statistik", 0x3498DB, 2, IsDeleted: false, MemberCount: 1),
            new SettingsRoleDto(seed.Mitglied, "Mitglied", null, 1, IsDeleted: false, MemberCount: 1),
            new SettingsRoleDto(seed.Gone, "Alt", null, 0, IsDeleted: true, MemberCount: 0),
        ]);
        settings.TrackingRoleId.ShouldBeNull();
        settings.MemberAccessRoleIds.ShouldBeEmpty();
        settings.Mode.ShouldBe(RoleSettingsMode.NoTrackingRole);
        settings.BotPermissions.ShouldBe(new BotPermissionCheckDto(CanManageRoles: false, IsAboveTrackingRole: null));
    }

    [Fact]
    public async Task GetSettings_WhenTrackingRoleWasDeleted_ShouldReportTrackingRoleDeleted()
    {
        var seed = await SeedAsync();
        await using (var db = Fixture.CreateDbContext())
        {
            await new RoleSettingsStore(db, _time).SetTrackingRoleAsync(seed.GuildId, PlatformKind.Discord, seed.Statistik, seed.AdminId, Ct);
            await db.Roles.Where(r => r.Id == seed.Statistik).ExecuteUpdateAsync(s => s.SetProperty(r => r.DeletedAt, TestTime.Now), Ct);
        }

        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await SendAsync(http, "GET", SettingsPath);

        (await response.Content.ReadAsStringAsync(Ct)).ShouldContain("\"mode\":\"trackingRoleDeleted\"");
    }

    [Theory]
    [InlineData("/api/admin/settings/Fluxer")]
    [InlineData("/api/admin/settings/Teams")]
    [InlineData("/api/admin/settings/0")]
    [InlineData("/api/admin/settings/1")]
    public async Task GetSettings_WhenThePlatformHasNoSyncedGuild_ShouldReturn404(string path)
    {
        await SeedAsync();
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await SendAsync(http, "GET", path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetPreview_ShouldCountTheMembersHoldingAnyOfTheRoles()
    {
        var seed = await SeedAsync();
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var preview = await http.GetFromJsonAsync<RolePreviewDto>(
            new Uri($"{SettingsPath}/preview?roleIds={seed.Statistik}&roleIds={seed.Mitglied}", UriKind.Relative), Ct);

        // Without a tracking role nobody is tracked, the Administrator included, so nobody stops being tracked.
        preview.ShouldBe(new RolePreviewDto(Members: 2, NewlyTracked: 2, NoLongerTracked: 0));
    }

    [Fact]
    public async Task PutTrackingRole_ShouldStoreTheRoleWithTheAdminAndTheTime()
    {
        var seed = await SeedAsync();
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await PutAsync(http, $"{SettingsPath}/tracking-role", new TrackingRoleRequest(seed.Statistik));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        await using var db = Fixture.CreateDbContext();
        var row = (await db.RoleSettings.AsNoTracking().ToListAsync(Ct)).ShouldHaveSingleItem();
        (row.RoleId, row.Purpose, row.ChangedByUserId, row.ChangedAt).ShouldBe((seed.Statistik, RoleSettingPurpose.Tracking, seed.AdminId, TestTime.Now));
    }

    [Fact]
    public async Task PutTrackingRole_WhenRoleIdIsNull_ShouldClearTheTrackingRole()
    {
        var seed = await SeedAsync();
        await using (var db = Fixture.CreateDbContext())
        {
            await new RoleSettingsStore(db, _time).SetTrackingRoleAsync(seed.GuildId, PlatformKind.Discord, seed.Statistik, seed.AdminId, Ct);
        }

        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await PutAsync(http, $"{SettingsPath}/tracking-role", new TrackingRoleRequest(null));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var settings = await http.GetFromJsonAsync<RoleSettingsDto>(new Uri(SettingsPath, UriKind.Relative), Ct);
        settings.ShouldNotBeNull().Mode.ShouldBe(RoleSettingsMode.NoTrackingRole);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task PutTrackingRole_WhenInvalidRole_ShouldReturn400(bool deleted)
    {
        var seed = await SeedAsync();
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await PutAsync(http, $"{SettingsPath}/tracking-role", new TrackingRoleRequest(deleted ? seed.Gone : Guid.NewGuid()));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using var db = Fixture.CreateDbContext();
        (await db.RoleSettings.AnyAsync(Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task PutTrackingRole_WithoutAntiforgeryToken_ShouldReturn400AndChangeNothing()
    {
        var seed = await SeedAsync();
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await http.PutAsJsonAsync(new Uri($"{SettingsPath}/tracking-role", UriKind.Relative), new TrackingRoleRequest(seed.Statistik), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using var db = Fixture.CreateDbContext();
        (await db.RoleSettings.AnyAsync(Ct)).ShouldBeFalse();
    }

    [Fact]
    public async Task PutTrackingRole_WhenAnotherAdminSavesAtTheSameTime_ShouldReturn409()
    {
        var seed = await SeedAsync();
        var interceptor = new CompetingWriteInterceptor(EntityState.Added, async () =>
        {
            await using var other = Fixture.CreateDbContext();
            await new RoleSettingsStore(other, _time).SetTrackingRoleAsync(seed.GuildId, PlatformKind.Discord, seed.Mitglied, seed.AdminId, Ct);
        });
        await using var factory = CreateFactory();
        await using var withInterceptor = factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(
            services => services.ConfigureDbContext<ApolloDbContext>(options => options.AddInterceptors(interceptor))));
        using var http = withInterceptor.CreateClient(new() { BaseAddress = ApolloWebApplicationFactory.BaseAddress, AllowAutoRedirect = false });
        await SignInAsync(http);

        using var response = await PutAsync(http, $"{SettingsPath}/tracking-role", new TrackingRoleRequest(seed.Statistik));

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        await using var db = Fixture.CreateDbContext();
        (await db.RoleSettings.AsNoTracking().SingleAsync(Ct)).RoleId.ShouldBe(seed.Mitglied);
    }

    [Fact]
    public async Task PutMemberAccessRoles_ShouldStoreExactlyTheSet()
    {
        var seed = await SeedAsync();
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await PutAsync(http, $"{SettingsPath}/member-access-roles", new MemberAccessRolesRequest([seed.Statistik, seed.Mitglied]));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var settings = await http.GetFromJsonAsync<RoleSettingsDto>(new Uri(SettingsPath, UriKind.Relative), Ct);
        settings.ShouldNotBeNull().MemberAccessRoleIds.ShouldBe([seed.Statistik, seed.Mitglied], ignoreOrder: true);
    }

    [Fact]
    public async Task PutMemberAccessRoles_WhenARoleIsDeleted_ShouldReturn400()
    {
        var seed = await SeedAsync();
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await PutAsync(http, $"{SettingsPath}/member-access-roles", new MemberAccessRolesRequest([seed.Mitglied, seed.Gone]));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetHistory_ShouldReturnTheNewestChangesWithTheirAuthor()
    {
        var seed = await SeedAsync();
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);
        await PutAsync(http, $"{SettingsPath}/tracking-role", new TrackingRoleRequest(seed.Statistik));
        _time.Advance(TimeSpan.FromMinutes(1));
        await PutAsync(http, $"{SettingsPath}/tracking-role", new TrackingRoleRequest(seed.Mitglied));

        var history = await http.GetFromJsonAsync<RoleSettingChangeDto[]>(new Uri($"{SettingsPath}/history?take=2", UriKind.Relative), Ct);

        history.ShouldBe(
        [
            new RoleSettingChangeDto(TestTime.Now.AddMinutes(1), "Nelly", RoleSettingKind.Tracking, RoleSettingChangeAction.Removed, "Statistik"),
            new RoleSettingChangeDto(TestTime.Now.AddMinutes(1), "Nelly", RoleSettingKind.Tracking, RoleSettingChangeAction.Added, "Mitglied"),
        ], ignoreOrder: true);
    }

    private ApolloWebApplicationFactory CreateFactory() =>
        new() { ConnectionString = Fixture.ConnectionString, TimeProvider = _time };

    /// <summary>
    /// The configured guild, synced: Nelly (the signed-in user), Bob holding Mitglied, a bot holding Statistik, and the
    /// deleted role Alt.
    /// </summary>
    private async Task<Seed> SeedAsync(bool isAdministrator = true)
    {
        await using var db = Fixture.CreateDbContext();
        var ids = new PlatformIdResolver(db, new PlatformIdCache(), NullLogger<PlatformIdResolver>.Instance);
        var guild = await new GuildBuilder().For(ApolloWebApplicationFactory.Guild).Named("Apollo").SeedAsync(db, ids, Ct);
        await new UserBuilder().For(_discordUser).Named("Nelly").SeedAsync(db, ids, Ct);
        var admin = await new GuildMemberBuilder().For(_discordUser, ApolloWebApplicationFactory.Guild).AsAdministrator(isAdministrator).SeedAsync(db, ids, Ct);
        var statistik = await new RoleBuilder().For(_statistik, ApolloWebApplicationFactory.Guild).Named("Statistik").AtPosition(2).WithColor(0x3498DB).SeedAsync(db, ids, Ct);
        var mitglied = await new RoleBuilder().For(_mitglied, ApolloWebApplicationFactory.Guild).Named("Mitglied").AtPosition(1).SeedAsync(db, ids, Ct);
        var gone = await new RoleBuilder().For(_gone, ApolloWebApplicationFactory.Guild).Named("Alt").AtPosition(0).DeletedAt(TestTime.Now).SeedAsync(db, ids, Ct);
        var bob = await new GuildMemberBuilder().For(new PlatformRef(PlatformKind.Discord, "600000000000000001"), ApolloWebApplicationFactory.Guild).SeedAsync(db, ids, Ct);
        var bot = await new GuildMemberBuilder().For(new PlatformRef(PlatformKind.Discord, "600000000000000002"), ApolloWebApplicationFactory.Guild).AsBot().SeedAsync(db, ids, Ct);
        var carol = await new GuildMemberBuilder().For(new PlatformRef(PlatformKind.Discord, "600000000000000003"), ApolloWebApplicationFactory.Guild).SeedAsync(db, ids, Ct);
        db.MemberRoles.AddRange(
            new MemberRole { GuildId = guild.Id, UserId = bob.UserId, RoleId = mitglied.Id },
            new MemberRole { GuildId = guild.Id, UserId = bot.UserId, RoleId = statistik.Id },
            new MemberRole { GuildId = guild.Id, UserId = carol.UserId, RoleId = statistik.Id });
        await db.SaveChangesAsync(Ct);
        return new Seed(guild.Id, admin.UserId, statistik.Id, mitglied.Id, gone.Id);
    }

    /// <summary>Signs in through <c>SignInCompletion</c> with a browser-session cookie, so the test clock cannot expire it.</summary>
    private static async Task SignInAsync(HttpClient http)
    {
        using var response = await http.GetAsync(new Uri($"{TestSignInEndpoints.PlatformSignInPath}?id={DiscordUserId}", UriKind.Relative), Ct);
        response.EnsureSuccessStatusCode();
    }

    private static async Task<HttpResponseMessage> PutAsync<T>(HttpClient http, string path, T body)
    {
        var antiforgery = await http.GetFromJsonAsync<AntiforgeryTokenDto>(new Uri("/auth/antiforgery", UriKind.Relative), Ct);
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = JsonContent.Create(body) };
        request.Headers.Add(antiforgery!.HeaderName, antiforgery.RequestToken);
        return await http.SendAsync(request, Ct);
    }

    private static Task<HttpResponseMessage> SendAsync(HttpClient http, string method, string path) =>
        method == "PUT"
            ? http.PutAsJsonAsync(new Uri(path, UriKind.Relative), new { }, Ct)
            : http.GetAsync(new Uri(path, UriKind.Relative), Ct);

    private sealed record Seed(Guid GuildId, Guid AdminId, Guid Statistik, Guid Mitglied, Guid Gone);
}
