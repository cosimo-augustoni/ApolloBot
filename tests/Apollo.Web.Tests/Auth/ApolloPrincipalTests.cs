using System.Security.Claims;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
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
        var principal = ApolloPrincipal.Create(_userId, PlatformKind.Discord, "Nelly", AvatarUrl, TestTime.Now);

        var identity = principal.Identities.ShouldHaveSingleItem();
        identity.AuthenticationType.ShouldBe(AuthSchemes.Session);
        identity.Name.ShouldBe("Nelly");
        identity.Claims.Select(c => (c.Type, c.Value)).ShouldBe(
        [
            (ApolloClaimTypes.UserId, _userId.ToString()),
            (ApolloClaimTypes.Platform, "Discord"),
            (ApolloClaimTypes.DisplayName, "Nelly"),
            (ApolloClaimTypes.AvatarUrl, AvatarUrl),
            (ApolloPrincipal.SignedInAtClaimType, "2026-01-15T09:00:00.0000000+00:00"),
        ], ignoreOrder: true);
        identity.Claims.ShouldNotContain(c => c.Type == ClaimTypes.NameIdentifier);
    }

    [Fact]
    public void CreatePrincipal_WhenUserHasNoAvatar_ShouldHaveNoAvatarClaim()
    {
        var principal = ApolloPrincipal.Create(_userId, PlatformKind.Discord, "Nelly", avatarUrl: null, TestTime.Now);

        principal.FindFirst(ApolloClaimTypes.AvatarUrl).ShouldBeNull();
    }

    [Theory]
    [InlineData(AvatarUrl)]
    [InlineData(null)]
    public void ToCurrentUser_ShouldReadInternalClaims(string? avatarUrl)
    {
        var principal = ApolloPrincipal.Create(_userId, PlatformKind.Fluxer, "Nelly", avatarUrl, TestTime.Now);

        ApolloPrincipal.ToCurrentUser(principal).ShouldBe(new CurrentUserDto(_userId, "Fluxer", "Nelly", avatarUrl));
    }

    [Fact]
    public void CreatePrincipal_ShouldHaveNoAdminClaimBeforeTheFirstAccessCheck()
    {
        var principal = ApolloPrincipal.Create(_userId, PlatformKind.Discord, "Nelly", null, TestTime.Now);

        principal.HasClaim(c => c.Type == ApolloClaimTypes.Admin).ShouldBeFalse();
        ApolloPrincipal.ReadSession(principal).ShouldBe(new SessionClaims(_userId, PlatformKind.Discord, TestTime.Now, AccessCheckedAt: null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void WithAccess_ShouldSetAdminClaimAndCheckTimeAndKeepTheRest(bool isAdministrator)
    {
        var principal = ApolloPrincipal.Create(_userId, PlatformKind.Discord, "Nelly", AvatarUrl, TestTime.Now);

        var checkedTwice = ApolloPrincipal.WithAccess(
            ApolloPrincipal.WithAccess(principal, !isAdministrator, TestTime.Now.AddMinutes(1)), isAdministrator, TestTime.Now.AddMinutes(6));

        checkedTwice.FindAll(ApolloClaimTypes.Admin).Select(c => c.Value).ShouldBe(isAdministrator ? ["true"] : []);
        ApolloPrincipal.ReadSession(checkedTwice).ShouldBe(new SessionClaims(_userId, PlatformKind.Discord, TestTime.Now, TestTime.Now.AddMinutes(6)));
        ApolloPrincipal.ToCurrentUser(checkedTwice).ShouldBe(new CurrentUserDto(_userId, "Discord", "Nelly", AvatarUrl, isAdministrator));
        checkedTwice.Identity!.AuthenticationType.ShouldBe(AuthSchemes.Session);
        checkedTwice.Identity.Name.ShouldBe("Nelly");
    }

    [Fact]
    public void ReadSession_WhenSignInTimeIsMissing_ShouldReturnNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ApolloClaimTypes.UserId, _userId.ToString()), new Claim(ApolloClaimTypes.Platform, "Discord")], AuthSchemes.Session));

        ApolloPrincipal.ReadSession(principal).ShouldBeNull();
    }
}
