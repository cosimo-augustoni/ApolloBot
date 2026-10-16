namespace Apollo.Platform.Abstractions.Identity;

/// <summary>
/// The user a platform login identified, in neutral form (scope §6.1). The web host maps <see cref="User"/> to the
/// internal user; nothing else from the login is kept.
/// </summary>
/// <param name="User">The platform user that logged in.</param>
/// <param name="DisplayName">The name the platform shows for the user (global name, else username).</param>
/// <param name="AvatarUrl">The user's avatar image, or <see langword="null"/> when they have none.</param>
public sealed record PlatformIdentity(PlatformRef User, string DisplayName, string? AvatarUrl);
