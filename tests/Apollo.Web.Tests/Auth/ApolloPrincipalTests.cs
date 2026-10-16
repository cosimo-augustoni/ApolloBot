using System.Security.Claims;
using Apollo.Platform.Abstractions;
using Apollo.Web.Auth;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Tests.Auth;

public sealed class ApolloPrincipalTests
{
    private static readonly Guid _userId = Guid.Parse("0b6f3d4e-1a2b-4c3d-8e9f-a0b1c2d3e4f5");
    private const string AvatarUrl = "https://cdn.discordapp.com/avatars/42/abc.png";

    [Fact]
    public void CreatePrincipal_ShouldContainOnlyInternalClaims()
    {
        var principal = ApolloPrincipal.Create(_userId, PlatformKind.Discord, "Nelly", AvatarUrl);

        var identity = principal.Identities.ShouldHaveSingleItem();
        identity.AuthenticationType.ShouldBe(AuthSchemes.Session);
        identity.Name.ShouldBe("Nelly");
        identity.Claims.Select(c => (c.Type, c.Value)).ShouldBe(
        [
            (ApolloClaimTypes.UserId, _userId.ToString()),
            (ApolloClaimTypes.Platform, "Discord"),
            (ApolloClaimTypes.DisplayName, "Nelly"),
            (ApolloClaimTypes.AvatarUrl, AvatarUrl),
        ], ignoreOrder: true);
        identity.Claims.ShouldNotContain(c => c.Type == ClaimTypes.NameIdentifier);
    }

    [Fact]
    public void CreatePrincipal_WhenUserHasNoAvatar_ShouldHaveNoAvatarClaim()
    {
        var principal = ApolloPrincipal.Create(_userId, PlatformKind.Discord, "Nelly", avatarUrl: null);

        principal.FindFirst(ApolloClaimTypes.AvatarUrl).ShouldBeNull();
    }

    [Theory]
    [InlineData(AvatarUrl)]
    [InlineData(null)]
    public void ToCurrentUser_ShouldReadInternalClaims(string? avatarUrl)
    {
        var principal = ApolloPrincipal.Create(_userId, PlatformKind.Fluxer, "Nelly", avatarUrl);

        ApolloPrincipal.ToCurrentUser(principal).ShouldBe(new CurrentUserDto(_userId, "Fluxer", "Nelly", avatarUrl));
    }
}
