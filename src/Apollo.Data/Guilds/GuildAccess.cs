namespace Apollo.Data.Guilds;

/// <summary>What the synced guild state (#13) says about a user in one guild (scope §6.2).</summary>
public enum GuildAccess
{
    /// <summary>Not a current member: never seen in the guild, left it, or the guild was never synced.</summary>
    NotMember,

    /// <summary>A current member without the Administrator permission.</summary>
    Member,

    /// <summary>A current member with the Administrator permission, through a role or as the guild owner.</summary>
    Administrator,
}
