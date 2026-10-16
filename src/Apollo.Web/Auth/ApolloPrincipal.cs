using System.Globalization;
using System.Security.Claims;
using Apollo.Platform.Abstractions;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Auth;

/// <summary>
/// The session principal (scope §6.1): internal user ID, login platform and display name. No platform ID as the
/// identity and no token of any kind.
/// </summary>
public static class ApolloPrincipal
{
    public static ClaimsPrincipal Create(Guid userId, PlatformKind platform, string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        Claim[] claims =
        [
            new(ApolloClaimTypes.UserId, userId.ToString()),
            new(ApolloClaimTypes.Platform, platform.ToString()),
            new(ApolloClaimTypes.DisplayName, displayName),
        ];

        return new ClaimsPrincipal(new ClaimsIdentity(claims, AuthSchemes.Session, ApolloClaimTypes.DisplayName, ClaimTypes.Role));
    }

    /// <exception cref="InvalidOperationException">The principal is not a session principal.</exception>
    public static CurrentUserDto ToCurrentUser(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return new CurrentUserDto(
            Guid.Parse(Required(principal, ApolloClaimTypes.UserId), CultureInfo.InvariantCulture),
            Required(principal, ApolloClaimTypes.Platform),
            Required(principal, ApolloClaimTypes.DisplayName));
    }

    private static string Required(ClaimsPrincipal principal, string claimType) =>
        principal.FindFirst(claimType)?.Value
            ?? throw new InvalidOperationException($"The session principal has no '{claimType}' claim.");
}
