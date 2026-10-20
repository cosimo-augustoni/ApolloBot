namespace Apollo.Domain.Presence;

/// <summary>
/// A session that started at <paramref name="StartedAt"/> closes at <paramref name="At"/>, with its
/// <paramref name="Segments"/> in order. It is stored only if <see cref="IsLongEnough"/>, together with every segment.
/// </summary>
public sealed record PresenceSessionClosed(
    Guid UserId,
    PresenceActivity Activity,
    DateTimeOffset StartedAt,
    DateTimeOffset At,
    IReadOnlyList<PresenceSegment> Segments)
    : PresenceChange(UserId, Activity, At)
{
    /// <summary>
    /// Whether the session lasted at least <see cref="PresenceSessionStateMachine.MinimumDuration"/> (scope §5.1, D1).
    /// </summary>
    public bool IsLongEnough => At - StartedAt >= PresenceSessionStateMachine.MinimumDuration;

    public bool Equals(PresenceSessionClosed? other) =>
        other is not null
        && base.Equals(other)
        && StartedAt == other.StartedAt
        && Segments.SequenceEqual(other.Segments);

    public override int GetHashCode() => HashCode.Combine(base.GetHashCode(), StartedAt, Segments.Count);
}
