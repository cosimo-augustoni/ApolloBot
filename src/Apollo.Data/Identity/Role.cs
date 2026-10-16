namespace Apollo.Data.Identity;

/// <summary>
/// The internal identity of a role, scoped to its internal guild. Platform IDs map to it through
/// <see cref="RolePlatformId"/> (scope §4.2).
/// </summary>
public sealed class Role
{
    public Guid Id { get; init; }

    public Guid GuildId { get; init; }
}
