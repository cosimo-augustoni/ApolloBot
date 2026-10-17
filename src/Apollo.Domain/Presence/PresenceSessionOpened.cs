namespace Apollo.Domain.Presence;

/// <summary>
/// A session opens. Nothing is stored yet: open sessions live only in memory.
/// </summary>
public sealed record PresenceSessionOpened(Guid UserId, PresenceActivity Activity, DateTimeOffset At) : PresenceChange(UserId, Activity, At);
