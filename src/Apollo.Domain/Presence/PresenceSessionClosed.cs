namespace Apollo.Domain.Presence;

/// <summary>
/// A session that started at <paramref name="StartedAt"/> closes at <paramref name="At"/>. It is stored only if
/// <see cref="IsLongEnough"/>.
/// </summary>
public sealed record PresenceSessionClosed(Guid UserId, PresenceActivity Activity, DateTimeOffset StartedAt, DateTimeOffset At)
    : PresenceChange(UserId, Activity, At)
{
    /// <summary>
    /// Whether the session lasted at least <see cref="PresenceSessionStateMachine.MinimumDuration"/> (scope §5.1, D1).
    /// </summary>
    public bool IsLongEnough => At - StartedAt >= PresenceSessionStateMachine.MinimumDuration;
}
