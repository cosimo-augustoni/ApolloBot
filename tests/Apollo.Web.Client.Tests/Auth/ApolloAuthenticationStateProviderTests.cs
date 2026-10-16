using Apollo.Web.Client.Api;
using Apollo.Web.Client.Auth;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Tests.Auth;

public sealed class ApolloAuthenticationStateProviderTests
{
    [Fact]
    public async Task GetAuthenticationState_WhenLoggedIn_ShouldCarrySessionClaims()
    {
        var user = new CurrentUserDto(Guid.Parse("0b6f3d4e-1a2b-4c3d-8e9f-a0b1c2d3e4f5"), "Discord", "Nelly", "https://cdn.discordapp.com/avatars/42/abc.png");
        var provider = new ApolloAuthenticationStateProvider(new FixedAuthClient(user));

        var principal = (await provider.GetAuthenticationStateAsync()).User;

        principal.Identity!.IsAuthenticated.ShouldBeTrue();
        principal.Identity.Name.ShouldBe("Nelly");
        principal.FindFirst(ApolloClaimTypes.UserId)!.Value.ShouldBe(user.UserId.ToString());
        principal.FindFirst(ApolloClaimTypes.Platform)!.Value.ShouldBe("Discord");
        principal.FindFirst(ApolloClaimTypes.AvatarUrl)!.Value.ShouldBe(user.AvatarUrl);
    }

    [Fact]
    public async Task GetAuthenticationState_WhenUserHasNoAvatar_ShouldHaveNoAvatarClaim()
    {
        var provider = new ApolloAuthenticationStateProvider(new FixedAuthClient(new CurrentUserDto(Guid.NewGuid(), "Discord", "Nelly", null)));

        var principal = (await provider.GetAuthenticationStateAsync()).User;

        principal.FindFirst(ApolloClaimTypes.AvatarUrl).ShouldBeNull();
    }

    [Fact]
    public async Task GetAuthenticationState_WhenAnonymous_ShouldNotBeAuthenticated()
    {
        var provider = new ApolloAuthenticationStateProvider(new FixedAuthClient(null));

        (await provider.GetAuthenticationStateAsync()).User.Identity!.IsAuthenticated.ShouldBeFalse();
    }

    private sealed class FixedAuthClient(CurrentUserDto? user) : IAuthClient
    {
        public Task<CurrentUserDto?> GetCurrentUserAsync(CancellationToken cancellationToken) => Task.FromResult(user);

        public Task LogoutAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
