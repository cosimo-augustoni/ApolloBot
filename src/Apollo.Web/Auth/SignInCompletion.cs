using System.Security.Claims;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions.Identity;

namespace Apollo.Web.Auth;

/// <summary>
/// Turns a platform login into the session principal: the platform user is mapped to the internal user (created on
/// first login, scope §4.2), and only internal claims go into the session. Display name and avatar are refreshed in
/// the session on every login; the <c>users</c> snapshot columns belong to the guild sync (#13). The first access
/// check runs here (<see cref="AccessRevalidator"/>), so the session starts with its access claims (#30).
/// </summary>
public sealed class SignInCompletion(IPlatformIdResolver resolver, AccessRevalidator revalidator, TimeProvider time)
{
    public async Task<ClaimsPrincipal> CompleteAsync(PlatformIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var userId = await resolver.ResolveUserAsync(identity.User, cancellationToken);
        var principal = ApolloPrincipal.Create(userId, identity.User.Platform, identity.DisplayName, identity.AvatarUrl, time.GetUtcNow());

        // Never checked, so the result is a refreshed principal. Only a logout stored with a later time than this login
        // (clock skew between hosts) rejects it; then the unchecked principal is rejected again on its first request.
        var access = await revalidator.RevalidateAsync(principal, cancellationToken);
        return access.Principal ?? principal;
    }
}
