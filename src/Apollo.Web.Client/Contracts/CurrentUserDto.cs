namespace Apollo.Web.Client.Contracts;

/// <summary>Response of <c>GET /auth/me</c>: the logged-in user's session claims, all internal (scope §6.1).</summary>
/// <param name="UserId">The internal user ID, never a platform ID.</param>
/// <param name="Platform">The platform the user logged in with, e.g. <c>Discord</c>.</param>
/// <param name="DisplayName">The user's name on that platform at login.</param>
/// <param name="AvatarUrl">The user's avatar image on that platform at login, or <see langword="null"/> without one.</param>
public sealed record CurrentUserDto(Guid UserId, string Platform, string DisplayName, string? AvatarUrl);
