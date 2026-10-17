namespace Apollo.Domain.Presence;

/// <summary>
/// What the <see cref="PresenceSessionStateMachine"/> decided for one session of one user.
/// </summary>
/// <param name="UserId">The internal user ID.</param>
/// <param name="Activity">The session's activity.</param>
/// <param name="At">When the change happened.</param>
public abstract record PresenceChange(Guid UserId, PresenceActivity Activity, DateTimeOffset At);
