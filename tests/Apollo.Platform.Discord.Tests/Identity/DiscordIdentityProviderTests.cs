using System.Security.Claims;
using System.Text.Json;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Identity;
using Apollo.Platform.Discord.Identity;

namespace Apollo.Platform.Discord.Tests.Identity;

public sealed class DiscordIdentityProviderTests
{
    private readonly DiscordIdentityProvider _provider = new();

    [Fact]
    public void MapUser_WhenDiscordUserJsonGiven_ShouldReturnPlatformIdentity()
    {
        var user = Json("""{ "id": "80351110224678912", "username": "nelly", "global_name": "Nelly", "avatar": "8342729096ea3675442027381ff50dfe" }""");

        var identity = DiscordIdentityProvider.MapUser(user);

        identity.ShouldBe(new PlatformIdentity(
            new PlatformRef(PlatformKind.Discord, "80351110224678912"),
            "Nelly",
            "https://cdn.discordapp.com/avatars/80351110224678912/8342729096ea3675442027381ff50dfe.png"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"\"")]
    public void MapUser_WhenGlobalNameMissing_ShouldFallBackToUsername(string globalName)
    {
        var user = Json($$"""{ "id": "1", "username": "nelly", "global_name": {{globalName}}, "avatar": null }""");

        DiscordIdentityProvider.MapUser(user).DisplayName.ShouldBe("nelly");
    }

    [Fact]
    public void MapUser_WhenAvatarMissing_ShouldHaveNoAvatarUrl()
    {
        var user = Json("""{ "id": "1", "username": "nelly", "avatar": null }""");

        DiscordIdentityProvider.MapUser(user).AvatarUrl.ShouldBeNull();
    }

    [Fact]
    public void MapUser_WhenAvatarAnimated_ShouldUseGif()
    {
        var user = Json("""{ "id": "1", "username": "nelly", "avatar": "a_1269e74af4df7417b13759eae50c83dc" }""");

        DiscordIdentityProvider.MapUser(user).AvatarUrl.ShouldBe("https://cdn.discordapp.com/avatars/1/a_1269e74af4df7417b13759eae50c83dc.gif");
    }

    [Fact]
    public void GetIdentity_WhenPrincipalHasClaimsFromMapUser_ShouldReturnTheSameIdentity()
    {
        var identity = new PlatformIdentity(new PlatformRef(PlatformKind.Discord, "42"), "Nelly", "https://cdn.discordapp.com/avatars/42/abc.png");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(DiscordIdentityProvider.ToClaims(identity), DiscordIdentityProvider.Scheme));

        _provider.GetIdentity(principal).ShouldBe(identity);
    }

    [Fact]
    public void GetIdentity_WhenPrincipalHasNoDiscordUser_ShouldReturnNull()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "Nelly")], "Other"));

        _provider.GetIdentity(principal).ShouldBeNull();
    }

    [Fact]
    public void Provider_ShouldNameDiscordPlatformAndScheme()
    {
        _provider.Platform.ShouldBe(PlatformKind.Discord);
        _provider.SchemeName.ShouldBe("Discord");
    }

    private static JsonElement Json(string json) => JsonDocument.Parse(json).RootElement.Clone();
}
