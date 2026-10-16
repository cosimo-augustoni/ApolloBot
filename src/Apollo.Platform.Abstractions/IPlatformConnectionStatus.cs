namespace Apollo.Platform.Abstractions;

/// <summary>
/// The adapter's current gateway connection, read by the bot heartbeat and health check (#24). It follows the same
/// transitions the adapter reports as <see cref="Events.PlatformConnectionStateChanged"/>.
/// </summary>
public interface IPlatformConnectionStatus
{
    /// <summary>
    /// The configured guild the connection records.
    /// </summary>
    PlatformRef Guild { get; }

    /// <summary>
    /// The connection state as one consistent snapshot; read it once per decision instead of combining several reads.
    /// </summary>
    PlatformConnectionSnapshot Current { get; }
}
