namespace Apollo.Platform.Abstractions.Events;

/// <summary>
/// A user's ban was lifted.
/// </summary>
/// <param name="Guild">The guild the event happened in.</param>
/// <param name="OccurredAt">When it happened (see <see cref="PlatformEvent.OccurredAt"/>).</param>
/// <param name="User">The user.</param>
public sealed record PlatformMemberUnbanned(
    PlatformRef Guild,
    DateTimeOffset OccurredAt,
    PlatformRef User)
    : PlatformEvent(Guild, OccurredAt);
