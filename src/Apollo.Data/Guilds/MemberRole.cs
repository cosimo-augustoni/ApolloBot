namespace Apollo.Data.Guilds;

/// <summary>
/// A role a member currently holds. The guild state sync (#13) replaces these rows whenever the roles change; they
/// hold the current state only, no history.
/// </summary>
public sealed class MemberRole
{
    public Guid GuildId { get; init; }

    public Guid UserId { get; init; }

    public Guid RoleId { get; init; }
}
