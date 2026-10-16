namespace Apollo.Platform.Abstractions.Snapshots;

/// <summary>
/// A guild channel, category or thread.
/// </summary>
/// <param name="Channel">The channel.</param>
/// <param name="Name">The channel name.</param>
/// <param name="Type">The channel type.</param>
/// <param name="Category">The category the channel sits in; <see langword="null"/> for top-level channels and threads.</param>
/// <param name="ThreadParent">For threads, the channel the thread belongs to (both platforms report it).</param>
public sealed record PlatformChannel(
    PlatformRef Channel,
    string Name,
    PlatformChannelType Type,
    PlatformRef? Category,
    PlatformRef? ThreadParent);
