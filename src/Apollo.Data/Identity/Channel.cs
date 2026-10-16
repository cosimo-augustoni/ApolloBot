using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Data.Identity;

/// <summary>
/// The internal identity of a channel, category or thread, scoped to its internal guild. Platform IDs map to it
/// through <see cref="ChannelPlatformId"/> (scope §4.2). The snapshot columns are kept current by the guild state
/// sync (#13); a deleted channel keeps its row with <see cref="DeletedAt"/> set.
/// </summary>
public sealed class Channel
{
    public Guid Id { get; init; }

    public Guid GuildId { get; init; }

    public string? Name { get; set; }

    public PlatformChannelType? Type { get; set; }

    /// <summary>
    /// The category the channel sits in; <see langword="null"/> for top-level channels and threads.
    /// </summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// For threads, the channel the thread belongs to (scope §5.1, D2).
    /// </summary>
    public Guid? ThreadParentId { get; set; }

    public DateTimeOffset? DeletedAt { get; set; }
}
