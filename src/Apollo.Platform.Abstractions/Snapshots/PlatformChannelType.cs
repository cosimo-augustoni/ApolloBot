namespace Apollo.Platform.Abstractions.Snapshots;

/// <summary>
/// The kinds of guild channel ApolloBot distinguishes.
/// </summary>
public enum PlatformChannelType
{
    Text,
    Voice,
    Category,
    Announcement,
    Stage,
    Forum,
    Thread,

    /// <summary>
    /// A channel type the adapter does not map (yet).
    /// </summary>
    Other,
}
