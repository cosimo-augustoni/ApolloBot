namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A user left the guild (or was kicked or banned).
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="User">The user.</param>
public sealed record PlatformMemberLeft(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformRef User)
    : PlatformEvent(Guild, OccurredAt);
