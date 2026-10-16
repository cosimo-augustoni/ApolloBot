using Apollo.Platform.Abstractions.Snapshots;

namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A user joined the guild.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="Member">The new member.</param>
public sealed record PlatformMemberJoined(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformMember Member)
    : PlatformEvent(Guild, OccurredAt);
