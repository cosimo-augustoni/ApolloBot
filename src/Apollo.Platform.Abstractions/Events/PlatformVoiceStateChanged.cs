using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A voice connection joined, left, moved or changed mute/deaf/stream/video.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="State">The new state of the connection; <see cref="PlatformVoiceState.Channel"/> is <see langword="null"/> when it left voice.</param>
public sealed record PlatformVoiceStateChanged(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformVoiceState State)
    : PlatformEvent(Guild, OccurredAt);
