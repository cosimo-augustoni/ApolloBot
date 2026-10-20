namespace Apollo.Domain.Names;

/// <summary>
/// A member's three names, kept apart, as the platform reports them (#114).
/// </summary>
/// <param name="Nickname">The server nickname; <see langword="null"/> without one.</param>
/// <param name="GlobalName">The global display name; <see langword="null"/> without one.</param>
/// <param name="Username">The account name.</param>
public sealed record MemberNames(string? Nickname, string? GlobalName, string Username);
