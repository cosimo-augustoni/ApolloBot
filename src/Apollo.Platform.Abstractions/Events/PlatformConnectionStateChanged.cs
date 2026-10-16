namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// The gateway connection changed state. Reported for the configured guild.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="State">The new state.</param>
public sealed record PlatformConnectionStateChanged(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformConnectionState State)
    : PlatformEvent(Guild, OccurredAt);
