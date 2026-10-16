using System.Security.Claims;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions.Identity;

namespace Apollo.Web.Auth;

/// <summary>
/// Turns a platform login into the session principal: the platform user is mapped to the internal user (created on
/// first login, scope §4.2), and only internal claims go into the session.
/// </summary>
public sealed class SignInCompletion(IPlatformIdResolver resolver)
{
    public async Task<ClaimsPrincipal> CompleteAsync(PlatformIdentity identity, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(identity);

        var userId = await resolver.ResolveUserAsync(identity.User, cancellationToken);

        return ApolloPrincipal.Create(userId, identity.User.Platform, identity.DisplayName);
    }
}
