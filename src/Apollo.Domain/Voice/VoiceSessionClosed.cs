namespace Apollo.Domain.Voice;

/// <summary>
/// The user's open session and its current segment close.
/// </summary>
public sealed record VoiceSessionClosed(Guid UserId, DateTimeOffset At, VoiceSessionEndReason Reason) : VoiceChange(UserId, At);
