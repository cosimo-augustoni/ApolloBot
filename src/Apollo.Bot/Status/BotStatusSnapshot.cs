using Apollo.Platform.Abstractions;

namespace Apollo.Bot.Status;

/// <summary>
/// The values one heartbeat row records.
/// </summary>
/// <param name="Guild">The configured guild.</param>
/// <param name="Connected">Whether the gateway is connected.</param>
/// <param name="GatewayLatency">The latest gateway round-trip time; <see langword="null"/> while disconnected.</param>
/// <param name="LastEventAt">When the dispatcher last received an event; <see langword="null"/> before the first.</param>
/// <param name="OpenVoiceSessions">The voice sessions the voice recorder holds open.</param>
public sealed record BotStatusSnapshot(
    PlatformRef Guild,
    bool Connected,
    TimeSpan? GatewayLatency,
    DateTimeOffset? LastEventAt,
    int OpenVoiceSessions);
