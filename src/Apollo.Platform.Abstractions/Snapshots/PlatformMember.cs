namespace Apollo.Platform.Abstractions.Snapshots;

/// <summary>
/// A guild member as the platform reports it.
/// </summary>
/// <param name="User">The user.</param>
/// <param name="DisplayName">The name shown in the guild (nickname, else global name, else username).</param>
/// <param name="AvatarUrl">The avatar image URL, if the user has one.</param>
/// <param name="IsBot">Whether the user is a bot account.</param>
/// <param name="IsAdministrator">Whether the member has the Administrator permission (through any role or as owner).</param>
/// <param name="JoinedAt">When the member joined the guild, if the platform reports it.</param>
/// <param name="Roles">The roles the member holds, without the implicit @everyone role.</param>
public sealed record PlatformMember(
    PlatformRef User,
    string DisplayName,
    string? AvatarUrl,
    bool IsBot,
    bool IsAdministrator,
    DateTimeOffset? JoinedAt,
    IReadOnlyList<PlatformRef> Roles);
