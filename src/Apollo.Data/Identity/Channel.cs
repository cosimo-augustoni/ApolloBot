namespace Apollo.Data.Identity;

/// <summary>
/// The internal identity of a channel, scoped to its internal guild. Platform IDs map to it through
/// <see cref="ChannelPlatformId"/> (scope §4.2).
/// </summary>
public sealed class Channel
{
    public Guid Id { get; init; }

    public Guid GuildId { get; init; }
}
