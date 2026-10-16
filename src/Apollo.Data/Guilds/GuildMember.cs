namespace Apollo.Data.Guilds;

/// <summary>
/// The guild-scoped facts about a member (scope §5.1, §6.2), kept current by the guild state sync (#13). A member who
/// left keeps the row with <see cref="LeftAt"/> set; the <see cref="Identity.User"/> stays too.
/// </summary>
public sealed class GuildMember
{
    public Guid GuildId { get; init; }

    public Guid UserId { get; init; }

    public DateTimeOffset? JoinedAt { get; set; }

    /// <summary>
    /// Whether the member has the Administrator permission, through a role or as the guild owner. Computed by the sync
    /// from <c>member_roles</c>, not taken from a library cache (docs/platform-parity.md).
    /// </summary>
    public bool IsAdministrator { get; set; }

    public bool IsBot { get; set; }

    public DateTimeOffset? LeftAt { get; set; }
}
