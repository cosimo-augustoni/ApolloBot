using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A role was created.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Role">The new role.</param>
public sealed record PlatformRoleCreated(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformRole Role)
    : PlatformEvent(Guild, OccurredAt);
