using System.Net;
using System.Net.Http.Json;
using Apollo.Web.Client.Contracts;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Apollo.Web.Tests.Auth;

public sealed class AuthEndpointsTests(ApolloWebApplicationFactory factory) : IClassFixture<ApolloWebApplicationFactory>
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Login_ShouldChallengeWithIdentifyScopeOnly()
    {
        using var http = factory.CreateHttpsClient();

        using var response = await http.GetAsync(new Uri("/auth/login", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!;
        location.GetLeftPart(UriPartial.Path).ShouldBe("https://discord.com/oauth2/authorize");
        var query = QueryHelpers.ParseQuery(location.Query);
        query["scope"].ToString().ShouldBe("identify");
        query["response_type"].ToString().ShouldBe("code");
        query["client_id"].ToString().ShouldBe(ApolloWebApplicationFactory.OAuthClientId);
        query["redirect_uri"].ToString().ShouldBe("https://localhost/signin-discord");
        query["state"].ToString().ShouldNotBeNullOrWhiteSpace();
        query["code_challenge"].ToString().ShouldNotBeNullOrWhiteSpace();
        query["code_challenge_method"].ToString().ShouldBe("S256");
    }

    [Fact]
    public async Task Login_WhenBehindReverseProxy_ShouldUseForwardedHostInRedirectUri()
    {
        using var http = factory.CreateClient(new() { BaseAddress = new Uri("http://apollo-web/"), AllowAutoRedirect = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/auth/login");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-Host", "apollo.example.ch");
        request.Headers.Add("X-Forwarded-For", "203.0.113.7");

        using var response = await http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        QueryHelpers.ParseQuery(response.Headers.Location!.Query)["redirect_uri"].ToString()
            .ShouldBe("https://apollo.example.ch/signin-discord");
    }

    [Theory]
    [InlineData("/me", "/me")]
    [InlineData("https://evil.example/", "/")]
    [InlineData("//evil.example/", "/")]
    public async Task Login_WhenReturnUrlExternal_ShouldIgnoreIt(string returnUrl, string expectedReturnUrl)
    {
        using var http = factory.CreateHttpsClient();

        using var response = await http.GetAsync(new Uri($"/auth/login?returnUrl={Uri.EscapeDataString(returnUrl)}", UriKind.Relative), Ct);

        var state = QueryHelpers.ParseQuery(response.Headers.Location!.Query)["state"].ToString();
        var options = factory.Services.GetRequiredService<IOptionsMonitor<OAuthOptions>>().Get("Discord");
        var properties = options.StateDataFormat.Unprotect(state).ShouldNotBeNull();
        properties.RedirectUri.ShouldBe($"/auth/complete?returnUrl={Uri.EscapeDataString(expectedReturnUrl)}");
    }

    [Fact]
    public async Task Login_WhenPlatformHasNoProvider_ShouldReturn404()
    {
        using var http = factory.CreateHttpsClient();

        using var response = await http.GetAsync(new Uri("/auth/login?platform=fluxer", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Me_WhenAnonymous_ShouldReturn401()
    {
        using var http = factory.CreateHttpsClient();

        using var response = await http.GetAsync(new Uri("/auth/me", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task Me_WhenAuthenticated_ShouldReturnInternalClaims()
    {
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        var me = await http.GetFromJsonAsync<CurrentUserDto>(new Uri("/auth/me", UriKind.Relative), Ct);

        me.ShouldBe(new CurrentUserDto(TestSignInEndpoints.UserId, "Discord", TestSignInEndpoints.DisplayName, TestSignInEndpoints.AvatarUrl));
    }

    [Fact]
    public async Task SessionCookie_ShouldBeHttpOnlySecureLax()
    {
        using var http = factory.CreateHttpsClient();

        using var response = await http.GetAsync(new Uri(TestSignInEndpoints.SessionSignInPath, UriKind.Relative), Ct);

        var cookie = response.Headers.GetValues("Set-Cookie").ShouldHaveSingleItem();
        cookie.ShouldStartWith(".Apollo.Session=");
        cookie.ShouldContain("httponly", Case.Insensitive);
        cookie.ShouldContain("secure", Case.Insensitive);
        cookie.ShouldContain("samesite=lax", Case.Insensitive);
        cookie.ShouldContain("path=/", Case.Insensitive);
    }

    [Fact]
    public async Task Logout_ShouldClearCookie()
    {
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);
        var antiforgery = await http.GetFromJsonAsync<AntiforgeryTokenDto>(new Uri("/auth/antiforgery", UriKind.Relative), Ct);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/auth/logout");
        request.Headers.Add(antiforgery!.HeaderName, antiforgery.RequestToken);

        using var response = await http.SendAsync(request, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        response.Headers.GetValues("Set-Cookie").ShouldContain(c =>
            c.StartsWith(".Apollo.Session=;", StringComparison.Ordinal) && c.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
        using var me = await http.GetAsync(new Uri("/auth/me", UriKind.Relative), Ct);
        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WhenAntiforgeryTokenMissing_ShouldReturn400AndKeepSession()
    {
        using var http = factory.CreateHttpsClient();
        await SignInAsync(http);

        using var response = await http.PostAsync(new Uri("/auth/logout", UriKind.Relative), content: null, Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        using var me = await http.GetAsync(new Uri("/auth/me", UriKind.Relative), Ct);
        me.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Complete_WhenNoExternalLogin_ShouldRedirectHomeWithoutSession()
    {
        using var http = factory.CreateHttpsClient();

        using var response = await http.GetAsync(new Uri("/auth/complete?returnUrl=%2Fme", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.OriginalString.ShouldBe("/");
        using var me = await http.GetAsync(new Uri("/auth/me", UriKind.Relative), Ct);
        me.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApiRequest_WhenAnonymous_ShouldReturn401InsteadOfRedirect()
    {
        using var http = factory.CreateHttpsClient();

        using var response = await http.GetAsync(new Uri(TestSignInEndpoints.ProtectedApiPath, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        response.Headers.Location.ShouldBeNull();
    }

    [Fact]
    public async Task PageRequest_WhenAnonymous_ShouldRedirectToLoginWithReturnUrl()
    {
        using var http = factory.CreateHttpsClient();

        using var response = await http.GetAsync(new Uri(TestSignInEndpoints.ProtectedPagePath, UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.PathAndQuery.ShouldBe($"/auth/login?returnUrl={Uri.EscapeDataString(TestSignInEndpoints.ProtectedPagePath)}");
    }

    private static async Task SignInAsync(HttpClient http)
    {
        using var response = await http.GetAsync(new Uri(TestSignInEndpoints.SessionSignInPath, UriKind.Relative), Ct);
        response.EnsureSuccessStatusCode();
    }
}
