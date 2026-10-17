using System.Net;
using Apollo.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Tests.Auth;

/// <summary>
/// The access check when the database is unreachable: the request runs as anonymous, the session cookie stays, and the
/// next request checks again. Neither the app shell nor static files fail because of it.
/// </summary>
public sealed class AccessCheckFailureTests : IAsyncDisposable
{
    // Nothing listens on port 1: every query fails at once (the context has no retries, scope.md D17).
    private const string UnreachableDatabase = "Host=127.0.0.1;Port=1;Database=apollo;Timeout=1";

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private readonly ApolloWebApplicationFactory _factory;

    public AccessCheckFailureTests() =>
        _factory = new ApolloWebApplicationFactory { ConnectionString = UnreachableDatabase, TimeProvider = _time };

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AdminApi_WhenAccessCheckCannotReachTheDatabase_ShouldReturn401AndKeepTheCookie()
    {
        using var http = await SignedInClientWithDueCheckAsync();

        using var response = await http.GetAsync(new Uri("/api/admin/session", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        ShouldNotClearTheSessionCookie(response);
    }

    [Fact]
    public async Task AppShell_WhenAccessCheckCannotReachTheDatabase_ShouldStillBeServed()
    {
        using var http = await SignedInClientWithDueCheckAsync();

        using var response = await http.GetAsync(new Uri("/", UriKind.Relative), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe("text/html");
        ShouldNotClearTheSessionCookie(response);
    }

    public async ValueTask DisposeAsync() => await _factory.DisposeAsync();

    // /test/sign-in issues a session that was just checked (no DB); after the interval the next request checks again.
    private async Task<HttpClient> SignedInClientWithDueCheckAsync()
    {
        var http = _factory.CreateHttpsClient();
        using var signIn = await http.GetAsync(new Uri(TestSignInEndpoints.SessionSignInPath, UriKind.Relative), Ct);
        signIn.EnsureSuccessStatusCode();
        _time.Advance(TimeSpan.FromMinutes(5));
        return http;
    }

    private static void ShouldNotClearTheSessionCookie(HttpResponseMessage response) =>
        (response.Headers.TryGetValues("Set-Cookie", out var cookies) ? cookies : [])
            .ShouldNotContain(c => c.StartsWith(".Apollo.Session=", StringComparison.Ordinal));
}
