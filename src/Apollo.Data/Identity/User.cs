namespace Apollo.Data.Identity;

/// <summary>
/// The internal identity of a user. Platform IDs map to it through <see cref="UserPlatformId"/> (scope §4.2).
/// The snapshot columns are kept current by the guild state sync (#13); guild-scoped facts live in
/// <see cref="Guilds.GuildMember"/>.
/// </summary>
public sealed class User
{
    public Guid Id { get; init; }

    /// <summary>
    /// The name shown in the guild (nickname, else global name, else username).
    /// </summary>
    public string? DisplayName { get; set; }

    public string? AvatarUrl { get; set; }
}
