using System.Security.Claims;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Identity;

namespace Apollo.Web.Tests.Auth;

/// <summary>
/// A platform login without a remote OAuth server: <see cref="TestSignInEndpoints.ExternalSignInPath"/> signs in to the
/// external cookie with the claims this provider reads, as a real handler would after its callback.
/// </summary>
public sealed class FakeIdentityProvider : IPlatformIdentityProvider
{
    public const string Scheme = "Fake";

    public const string AvatarClaimType = "urn:test:avatar";

    public PlatformKind Platform => PlatformKind.Discord;

    public string SchemeName => Scheme;

    public static ClaimsIdentity CreateIdentity(string userId, string displayName, string? avatarUrl)
    {
        var identity = new ClaimsIdentity(Scheme);
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, userId));
        identity.AddClaim(new Claim(ClaimTypes.Name, displayName));
        if (avatarUrl is not null)
        {
            identity.AddClaim(new Claim(AvatarClaimType, avatarUrl));
        }

        return identity;
    }

    public PlatformIdentity? GetIdentity(ClaimsPrincipal externalPrincipal)
    {
        var identity = externalPrincipal.Identities.FirstOrDefault(i => i.AuthenticationType == Scheme);
        return identity is null
            ? null
            : new PlatformIdentity(
                new PlatformRef(Platform, identity.FindFirst(ClaimTypes.NameIdentifier)!.Value),
                identity.FindFirst(ClaimTypes.Name)!.Value,
                identity.FindFirst(AvatarClaimType)?.Value);
    }
}
