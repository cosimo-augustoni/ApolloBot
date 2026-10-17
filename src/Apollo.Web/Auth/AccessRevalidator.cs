using System.Security.Claims;
using Apollo.Data.Guilds;
using Apollo.Data.Sessions;
using Apollo.Platform.Abstractions.Identity;
using Microsoft.Extensions.Options;

namespace Apollo.Web.Auth;

/// <summary>
/// The access check of a session (#30, scope §6.2). Within <see cref="WebAuthOptions.RevalidationInterval"/> of the
/// last check the claims in the cookie are trusted and nothing is read. After it, the session is rejected if the user
/// logged out since it started (logout revocation); otherwise the admin claim is set from the synced guild state, and
/// only from there: the Administrator permission in the configured guild of the login platform (D8). No role
/// setting or configuration value can grant it. Called by <see cref="SignInCompletion"/> and on every request by the
/// session cookie's <c>OnValidatePrincipal</c>.
/// </summary>
public sealed class AccessRevalidator(
    GuildAccessReader guildAccess,
    UserSignOuts signOuts,
    IEnumerable<IPlatformIdentityProvider> providers,
    IOptions<WebAuthOptions> options,
    TimeProvider time)
{
    public async Task<AccessRevalidation> RevalidateAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        if (ApolloPrincipal.ReadSession(principal) is not { } session)
        {
            return AccessRevalidation.Rejected;
        }

        var now = time.GetUtcNow();
        // A check time in the future means the clock went back: check again rather than trust the claims for longer.
        if (session.AccessCheckedAt is { } checkedAt && checkedAt <= now && now - checkedAt < options.Value.RevalidationInterval)
        {
            return AccessRevalidation.Unchanged;
        }

        if (await signOuts.GetLastAsync(session.UserId, cancellationToken) is { } signedOutAt && session.SignedInAt <= signedOutAt)
        {
            return AccessRevalidation.Rejected;
        }

        var guild = providers.FirstOrDefault(p => p.Platform == session.Platform)?.Guild;
        var isAdministrator = guild is not null
            && await guildAccess.GetAsync(session.UserId, guild, cancellationToken) == GuildAccess.Administrator;

        return AccessRevalidation.Refreshed(ApolloPrincipal.WithAccess(principal, isAdministrator, now));
    }
}
