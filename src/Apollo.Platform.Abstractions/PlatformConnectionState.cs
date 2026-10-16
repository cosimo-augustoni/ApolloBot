namespace Apollo.Platform.Abstractions;

/// <summary>
/// The adapter's gateway connection state, reported through <see cref="Events.PlatformConnectionStateChanged"/>.
/// </summary>
public enum PlatformConnectionState
{
    /// <summary>
    /// A new session; a <see cref="Events.PlatformGuildAvailable"/> snapshot follows.
    /// </summary>
    Connected,

    /// <summary>
    /// The connection dropped. Events may be missed until it is back.
    /// </summary>
    Disconnected,

    /// <summary>
    /// The previous session was resumed and missed events were replayed, so no snapshot follows.
    /// </summary>
    Resumed,
}
