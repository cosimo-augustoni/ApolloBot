namespace Apollo.Domain.Voice;

/// <summary>
/// What the <see cref="VoiceSessionStateMachine"/> decided should be stored for one user.
/// </summary>
/// <param name="UserId">The internal user ID.</param>
/// <param name="At">When the change happened.</param>
public abstract record VoiceChange(Guid UserId, DateTimeOffset At);
