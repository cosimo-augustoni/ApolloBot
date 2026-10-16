namespace Apollo.Platform.Abstractions;

/// <summary>
/// The gateway connection at one moment, see <see cref="IPlatformConnectionStatus.Current"/>.
/// </summary>
/// <param name="IsConnected">
/// <see langword="true"/> after a connect or resume, <see langword="false"/> before the first one and after a disconnect.
/// </param>
/// <param name="StateChangedAt">
/// Since when <paramref name="IsConnected"/> has had its value; the adapter's start time before the first connect.
/// </param>
/// <param name="Latency">
/// The latest gateway round-trip time of the current session. <see langword="null"/> while disconnected, and after every
/// connect or resume until that session's first measurement (on Discord, the first heartbeat ack, within one gateway
/// heartbeat interval): a value from the previous session would say nothing about the new one.
/// </param>
public sealed record PlatformConnectionSnapshot(bool IsConnected, DateTimeOffset StateChangedAt, TimeSpan? Latency);
