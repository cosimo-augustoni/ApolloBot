using System.Net;
using Apollo.Web.Client.Api;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Tests.Api;

public sealed class HttpAuthClientTests
{
    private static readonly CurrentUserDto _user = new(Guid.Parse("0b6f3d4e-1a2b-4c3d-8e9f-a0b1c2d3e4f5"), "Discord", "Nelly", "https://cdn.discordapp.com/avatars/42/abc.png");

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task GetCurrentUser_WhenLoggedIn_ShouldReturnUserFromMe()
    {
        var handler = new StubHttpMessageHandler(_user);

        var user = await new HttpAuthClient(handler.CreateClient()).GetCurrentUserAsync(Ct);

        user.ShouldBe(_user);
        handler.Requests.ShouldHaveSingleItem().AbsolutePath.ShouldBe("/auth/me");
    }

    [Fact]
    public async Task GetCurrentUser_WhenAnonymous_ShouldReturnNull()
    {
        var handler = new StubHttpMessageHandler(_user).Respond("/auth/me", HttpStatusCode.Unauthorized);

        var user = await new HttpAuthClient(handler.CreateClient()).GetCurrentUserAsync(Ct);

        user.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrentUser_WhenServerFails_ShouldThrow()
    {
        var handler = new StubHttpMessageHandler(_user).Respond("/auth/me", HttpStatusCode.InternalServerError);

        await Should.ThrowAsync<HttpRequestException>(() => new HttpAuthClient(handler.CreateClient()).GetCurrentUserAsync(Ct));
    }

    [Fact]
    public async Task Logout_ShouldPostWithAntiforgeryTokenHeader()
    {
        var handler = new StubHttpMessageHandler(_user)
            .Respond("/auth/antiforgery", HttpStatusCode.OK, new AntiforgeryTokenDto("X-XSRF-TOKEN", "request-token"))
            .Respond("/auth/logout", HttpStatusCode.NoContent);

        await new HttpAuthClient(handler.CreateClient()).LogoutAsync(Ct);

        handler.Messages.Select(m => (m.Method, m.RequestUri!.AbsolutePath)).ShouldBe(
            [(HttpMethod.Get, "/auth/antiforgery"), (HttpMethod.Post, "/auth/logout")]);
        handler.Messages[1].Headers.GetValues("X-XSRF-TOKEN").ShouldBe(["request-token"]);
    }

    [Fact]
    public async Task Logout_WhenRejected_ShouldThrow()
    {
        var handler = new StubHttpMessageHandler(_user)
            .Respond("/auth/antiforgery", HttpStatusCode.OK, new AntiforgeryTokenDto("X-XSRF-TOKEN", "request-token"))
            .Respond("/auth/logout", HttpStatusCode.BadRequest);

        await Should.ThrowAsync<HttpRequestException>(() => new HttpAuthClient(handler.CreateClient()).LogoutAsync(Ct));
    }
}
