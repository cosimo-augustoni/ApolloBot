namespace Apollo.Data.Identity;

/// <summary>
/// The internal identity of a guild. Platform IDs map to it through <see cref="GuildPlatformId"/> (scope §4.2).
/// The snapshot columns are kept current by the guild state sync (#13); they are empty until its first run.
/// </summary>
public sealed class Guild
{
    public Guid Id { get; init; }

    public string? Name { get; set; }

    /// <summary>
    /// The user who owns the guild. The owner holds every permission, Administrator included.
    /// </summary>
    public Guid? OwnerUserId { get; set; }
}
