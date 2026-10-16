using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A role changed.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Role">The role after the change.</param>
public sealed record PlatformRoleUpdated(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformRole Role)
    : PlatformEvent(Guild, OccurredAt);
