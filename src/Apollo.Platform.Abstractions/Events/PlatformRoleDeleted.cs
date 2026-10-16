namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A role was deleted.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Role">The role.</param>
public sealed record PlatformRoleDeleted(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformRef Role)
    : PlatformEvent(Guild, OccurredAt);
