using Apollo.Platform.Abstractions;

namespace Apollo.Web.Auth;

/// <summary>The claims of a session principal that the access check needs (<see cref="ApolloPrincipal.ReadSession"/>).</summary>
/// <param name="UserId">The internal user.</param>
/// <param name="Platform">The platform the user logged in with.</param>
/// <param name="SignedInAt">When the login happened; a logout after it ends the session.</param>
/// <param name="AccessCheckedAt">When the access claims were last read from the database; <see langword="null"/> before the first check.</param>
public sealed record SessionClaims(Guid UserId, PlatformKind Platform, DateTimeOffset SignedInAt, DateTimeOffset? AccessCheckedAt);
