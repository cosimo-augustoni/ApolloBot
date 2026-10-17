using System.Security.Claims;

namespace Apollo.Web.Client.Contracts;

/// <summary>The claim types of the session principal, on the host (cookie) and in the client (auth state).</summary>
public static class ApolloClaimTypes
{
    /// <summary>The internal user ID (<see cref="Guid"/>).</summary>
    public const string UserId = "apollo:user_id";

    /// <summary>The platform of the login, e.g. <c>Discord</c>.</summary>
    public const string Platform = "apollo:platform";

    /// <summary>The display name; the standard name claim, so <c>Identity.Name</c> returns it.</summary>
    public const string DisplayName = ClaimTypes.Name;

    /// <summary>The avatar image URL from the platform; absent when the user has none.</summary>
    public const string AvatarUrl = "apollo:avatar_url";

    /// <summary>
    /// <c>true</c> while the user holds the Administrator permission in the configured guild (scope §6.2, D8); absent
    /// otherwise. The host sets it only from the synced guild state (<c>AccessRevalidator</c>), never from a setting.
    /// </summary>
    public const string Admin = "apollo:admin";
}
