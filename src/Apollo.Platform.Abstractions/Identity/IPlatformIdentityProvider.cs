using System.Security.Claims;

namespace Apollo.Platform.Abstractions.Identity;

/// <summary>
/// A platform's OAuth login (scope §6.1). The adapter registers an authentication scheme named
/// <see cref="SchemeName"/> that signs in to the host's default sign-in scheme (a temporary external cookie);
/// the web host challenges it and turns the result into a <see cref="PlatformIdentity"/> with
/// <see cref="GetIdentity"/>. No platform token leaves the adapter.
/// </summary>
public interface IPlatformIdentityProvider
{
    PlatformKind Platform { get; }

    /// <summary>The authentication scheme of this platform's OAuth handler.</summary>
    string SchemeName { get; }

    /// <summary>
    /// The guild the bot records on this platform (scope §4.3). Web access for a login with this provider is decided
    /// by the user's membership and permissions there (scope §6.2).
    /// </summary>
    PlatformRef Guild { get; }

    /// <summary>
    /// The identity in the principal this provider's scheme produced, or <see langword="null"/> when the principal
    /// does not carry one (e.g. it came from another scheme).
    /// </summary>
    PlatformIdentity? GetIdentity(ClaimsPrincipal externalPrincipal);
}
