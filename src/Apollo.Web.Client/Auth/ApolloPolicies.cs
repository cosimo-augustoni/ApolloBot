using Apollo.Web.Client.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace Apollo.Web.Client.Auth;

/// <summary>
/// The authorization policies (scope §6.2), defined once for the host (API endpoints) and the client (pages), which
/// both see the same claims: the host in the session cookie, the client through <c>/auth/me</c>.
/// </summary>
public static class ApolloPolicies
{
    /// <summary>
    /// Everything: all users' stats, server dashboard, bot status. Requires the admin claim, which the host sets only
    /// from the Administrator permission in the configured guild (D8). <c>Member</c> follows in #41.
    /// </summary>
    public const string Admin = "Admin";

    public static AuthorizationOptions AddApolloPolicies(this AuthorizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        options.AddPolicy(Admin, policy => policy.RequireAuthenticatedUser().RequireClaim(ApolloClaimTypes.Admin, "true"));
        return options;
    }
}
