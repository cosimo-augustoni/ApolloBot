using System.Net;
using System.Net.Http.Json;
using Apollo.Testing.Data;
using Apollo.Web.Client.Contracts;
using Microsoft.EntityFrameworkCore;

namespace Apollo.Web.Tests.Auth;

/// <summary>The login against a real database: the callback side of the flow and the session across a restart.</summary>
public sealed class LoginFlowTests(TimescaleFixture fixture) : TimescaleTest(fixture)
{
    private const string AvatarUrl = "https://cdn.discordapp.com/avatars/80351110224678912/abc.png";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Complete_WhenPlatformLoginSucceeded_ShouldMapUserSetSessionCookieAndRedirectToReturnUrl()
    {
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await GetAsync(http, $"{TestSignInEndpoints.ExternalSignInPath}?id=80351110224678912&name=Nelly&avatar={Uri.EscapeDataString(AvatarUrl)}");

        using var response = await GetAsync(http, "/auth/complete?returnUrl=%2Fme");

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/me");
        var setCookies = response.Headers.GetValues("Set-Cookie").ToList();
        setCookies.ShouldContain(c => c.StartsWith(".Apollo.Session=", StringComparison.Ordinal) && !c.StartsWith(".Apollo.Session=;", StringComparison.Ordinal));
        setCookies.ShouldContain(c => c.StartsWith(".Apollo.External=;", StringComparison.Ordinal));

        await using var db = Fixture.CreateDbContext();
        var mapping = await db.UserPlatformIds.AsNoTracking().SingleAsync(Ct);
        mapping.ExternalId.ShouldBe("80351110224678912");
        var me = await http.GetFromJsonAsync<CurrentUserDto>(new Uri("/auth/me", UriKind.Relative), Ct);
        me.ShouldBe(new CurrentUserDto(mapping.UserId, "Discord", "Nelly", AvatarUrl));
    }

    [Fact]
    public async Task SessionCookie_FromLogin_ShouldBeHttpOnlySecureLaxAndPersistent()
    {
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await GetAsync(http, $"{TestSignInEndpoints.ExternalSignInPath}?id=1&name=Nelly");

        using var response = await GetAsync(http, "/auth/complete");

        var cookie = response.Headers.GetValues("Set-Cookie").Where(c => c.StartsWith(".Apollo.Session=", StringComparison.Ordinal)).ShouldHaveSingleItem();
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("samesite=lax", Case.Insensitive);
        cookie.ShouldContain("path=/", Case.Insensitive);
        cookie.ShouldContain("expires=", Case.Insensitive);
    }

    [Fact]
    public async Task Logout_AfterLogin_ShouldClearSessionCookie()
    {
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await GetAsync(http, $"{TestSignInEndpoints.ExternalSignInPath}?id=1&name=Nelly");
        (await GetAsync(http, "/auth/complete")).StatusCode.ShouldBe(HttpStatusCode.Redirect);
        (await GetAsync(http, "/auth/me")).StatusCode.ShouldBe(HttpStatusCode.OK);
        var antiforgery = await http.GetFromJsonAsync<AntiforgeryTokenDto>(new Uri("/auth/antiforgery", UriKind.Relative), Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add(antiforgery!.HeaderName, antiforgery.RequestToken);

        using var response = await http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").ShouldContain(c =>
            c.StartsWith(".Apollo.Session=;", StringComparison.Ordinal) && c.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        (await GetAsync(http, "/auth/me")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Complete_WhenReturnUrlExternal_ShouldRedirectToRoot()
    {
        await using var factory = CreateFactory();
        using var http = factory.CreateHttpsClient();
        await GetAsync(http, $"{TestSignInEndpoints.ExternalSignInPath}?id=1&name=Nelly");

        using var response = await GetAsync(http, $"/auth/complete?returnUrl={Uri.EscapeDataString("https://evil.example/")}");

        response.Headers.Location!.OriginalString.ShouldBe("/");
    }

    [Fact]
    public async Task DataProtectionKeys_ShouldBePersistedInDatabase()
    {
        var cookies = new CookieContainer();
        await using (var before = CreateFactory())
        {
            using var http = CreateClient(before, cookies);
            (await GetAsync(http, TestSignInEndpoints.SessionSignInPath)).EnsureSuccessStatusCode();
        }

        await using (var db = Fixture.CreateDbContext())
        {
            (await db.DataProtectionKeys.AsNoTracking().CountAsync(Ct)).ShouldBeGreaterThan(0);
        }

        // A new host has no keys in memory: it can only read the cookie with the keys from the database.
        await using var afterRestart = CreateFactory();
        using var restarted = CreateClient(afterRestart, cookies);
        using var me = await GetAsync(restarted, "/auth/me");

        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    private ApolloWebApplicationFactory CreateFactory() =>
        new() { ConnectionString = Fixture.ConnectionString, PersistDataProtectionKeys = true };

    // The factory's own cookie handling is per client; this one shares a container across two hosts.
    private static HttpClient CreateClient(ApolloWebApplicationFactory factory, CookieContainer cookies)
    {
        var handler = new CookieContainerHandler(cookies) { InnerHandler = factory.Server.CreateHandler() };
        return new HttpClient(handler) { BaseAddress = ApolloWebApplicationFactory.BaseAddress };
    }

    private static Task<HttpResponseMessage> GetAsync(HttpClient http, string path) =>
        http.GetAsync(new Uri(path, UriKind.Relative), Ct);

    private sealed class CookieContainerHandler(CookieContainer cookies) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            var header = cookies.GetCookieHeader(uri);
            if (header.Length > 0)
            {
                request.Headers.Add("Cookie", header);
            }

            var response = await base.SendAsync(request, cancellationToken);
            if (response.Headers.TryGetValues("Set-Cookie", out var setCookies))
            {
                foreach (var setCookie in setCookies)
                {
                    cookies.SetCookies(uri, setCookie);
                }
            }

            return response;
        }
    }
}
