using System.Globalization;
using System.Security.Claims;
using Apollo.Platform.Abstractions;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Auth;

/// <summary>
/// The session principal (scope §6.1): internal user ID, login platform, display name and avatar URL as the platform
/// reported them at login. No platform ID as the identity and no token of any kind. The <c>users</c> snapshot columns
/// are the bot's (#13); the login does not write them.
/// </summary>
public static class ApolloPrincipal
{
    public static ClaimsPrincipal Create(Guid userId, PlatformKind platform, string displayName, string? avatarUrl)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        List<Claim> claims =
        [
            new(ApolloClaimTypes.UserId, userId.ToString()),
            new(ApolloClaimTypes.Platform, platform.ToString()),
            new(ApolloClaimTypes.DisplayName, displayName),
        ];
        if (avatarUrl is not null)
        {
            claims.Add(new Claim(ApolloClaimTypes.AvatarUrl, avatarUrl));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.Session, ApolloClaimTypes.DisplayName, ClaimTypes.Role));
    }

    /// <exception cref="InvalidOperationException">The principal is not a session principal.</exception>
    public static CurrentUserDto ToCurrentUser(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return new CurrentUserDto(
            Guid.Parse(Required(principal, ApolloClaimTypes.UserId), CultureInfo.InvariantCulture),
            Required(principal, ApolloClaimTypes.Platform),
            Required(principal, ApolloClaimTypes.DisplayName),
            principal.FindFirst(ApolloClaimTypes.AvatarUrl)?.Value);
    }

    private static string Required(ClaimsPrincipal principal, string claimType) =>
        principal.FindFirst(claimType)?.Value
            ?? throw new InvalidOperationException($"The session principal has no '{claimType}' claim.");
}
