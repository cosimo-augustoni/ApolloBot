namespace Apollo.Data.Identity;

/// <summary>
/// The internal identity of a guild. Platform IDs map to it through <see cref="GuildPlatformId"/> (scope §4.2).
/// </summary>
public sealed class Guild
{
    public Guid Id { get; init; }
}
