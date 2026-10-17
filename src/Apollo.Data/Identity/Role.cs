namespace Apollo.Data.Identity;

/// <summary>
/// The internal identity of a role, scoped to its internal guild. Platform IDs map to it through
/// <see cref="RolePlatformId"/> (scope §4.2). The snapshot columns are kept current by the guild state sync (#13); a
/// deleted role keeps its row with <see cref="DeletedAt"/> set.
/// </summary>
public sealed class Role
{
    public Guid Id { get; init; }

    public Guid GuildId { get; init; }

    public string? Name { get; set; }

    /// <summary>
    /// The role colour as 0xRRGGBB; <see langword="null"/> when the role has none.
    /// </summary>
    public int? Color { get; set; }

    /// <summary>
    /// The sort position; higher is listed first.
    /// </summary>
    public int Position { get; set; }

    /// <summary>
    /// Whether the role grants the Administrator permission. Members holding it get
    /// <see cref="Guilds.GuildMember.IsAdministrator"/>.
    /// </summary>
    public bool IsAdministrator { get; set; }

    /// <summary>
    /// Whether the role grants the Manage Roles permission, which the bot needs to give and take the tracking role
    /// (scope §5.1.2). Not set by Administrator alone; the admin page checks both.
    /// </summary>
    public bool ManagesRoles { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
