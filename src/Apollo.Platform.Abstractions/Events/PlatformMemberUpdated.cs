using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A member changed, e.g. their roles or display name. Carries the full member, roles included.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Member">The member after the change.</param>
public sealed record PlatformMemberUpdated(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformMember Member)
    : PlatformEvent(Guild, OccurredAt);
