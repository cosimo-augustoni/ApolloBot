using System.Globalization;
using System.Security.Claims;
using Apollo.Platform.Abstractions;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Auth;

/// <summary>
/// The session principal (scope §6.1): internal user ID, login platform, display name and avatar URL as the platform
/// reported them at login. No platform ID as the identity and no token of any kind. The <c>users</c> snapshot columns
/// are the bot's (#13); the login does not write them. The access claims (<see cref="ApolloClaimTypes.Admin"/>, the
/// check time) are added by <see cref="AccessRevalidator"/> through <see cref="WithAccess"/> (#30).
/// </summary>
public static class ApolloPrincipal
{
    /// <summary>When the user logged in (round-trip format). Kept on every refresh; a later logout ends the session.</summary>
    public const string SignedInAtClaimType = "apollo:signed_in_at";

    /// <summary>When the access claims were last read from the database (round-trip format).</summary>
    public const string AccessCheckedAtClaimType = "apollo:access_checked_at";

    private const string AdminClaimValue = "true";

    public static ClaimsPrincipal Create(Guid userId, PlatformKind platform, string displayName, string? avatarUrl, DateTimeOffset signedInAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        List<Claim> claims =
        [
            new(ApolloClaimTypes.UserId, userId.ToString()),
            new(ApolloClaimTypes.Platform, platform.ToString()),
            new(ApolloClaimTypes.DisplayName, displayName),
            new(SignedInAtClaimType, FormatTime(signedInAt)),
        ];
        if (avatarUrl is not null)
        {
            claims.Add(new Claim(ApolloClaimTypes.AvatarUrl, avatarUrl));
        }

        return CreatePrincipal(claims);
    }

    /// <summary>
    /// A copy of <paramref name="principal"/> with the result of an access check: the admin claim present exactly when
    /// <paramref name="isAdministrator"/>, and <paramref name="checkedAt"/> as the check time.
    /// </summary>
    public static ClaimsPrincipal WithAccess(ClaimsPrincipal principal, bool isAdministrator, DateTimeOffset checkedAt)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var claims = principal.Claims
            .Where(c => c.Type is not ApolloClaimTypes.Admin and not AccessCheckedAtClaimType)
            .Select(c => new Claim(c.Type, c.Value))
            .Append(new Claim(AccessCheckedAtClaimType, FormatTime(checkedAt)))
            .ToList();
        if (isAdministrator)
        {
            claims.Add(new Claim(ApolloClaimTypes.Admin, AdminClaimValue));
        }

        return CreatePrincipal(claims);
    }

    /// <summary>The claims the access check needs, or <see langword="null"/> when the principal is not a valid session principal.</summary>
    public static SessionClaims? ReadSession(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (!Guid.TryParse(principal.FindFirst(ApolloClaimTypes.UserId)?.Value, CultureInfo.InvariantCulture, out var userId)
            || !Enum.TryParse<PlatformKind>(principal.FindFirst(ApolloClaimTypes.Platform)?.Value, out var platform)
            || ParseTime(principal.FindFirst(SignedInAtClaimType)?.Value) is not { } signedInAt)
        {
            return null;
        }

        return new SessionClaims(userId, platform, signedInAt, ParseTime(principal.FindFirst(AccessCheckedAtClaimType)?.Value));
    }

    /// <exception cref="InvalidOperationException">The principal is not a session principal.</exception>
    public static CurrentUserDto ToCurrentUser(ClaimsPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        return new CurrentUserDto(
            Guid.Parse(Required(principal, ApolloClaimTypes.UserId), CultureInfo.InvariantCulture),
            Required(principal, ApolloClaimTypes.Platform),
            Required(principal, ApolloClaimTypes.DisplayName),
            principal.FindFirst(ApolloClaimTypes.AvatarUrl)?.Value,
            principal.HasClaim(ApolloClaimTypes.Admin, AdminClaimValue));
    }

    private static ClaimsPrincipal CreatePrincipal(IEnumerable<Claim> claims) =>
        new(new ClaimsIdentity(claims, AuthSchemes.Session, ApolloClaimTypes.DisplayName, ClaimTypes.Role));

    private static string FormatTime(DateTimeOffset time) => time.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    private static DateTimeOffset? ParseTime(string? value) =>
        DateTimeOffset.TryParseExact(value, "O", CultureInfo.InvariantCulture, DateTimeStyles.None, out var time) ? time : null;

    private static string Required(ClaimsPrincipal principal, string claimType) =>
        principal.FindFirst(claimType)?.Value
            ?? throw new InvalidOperationException($"The session principal has no '{claimType}' claim.");
}
