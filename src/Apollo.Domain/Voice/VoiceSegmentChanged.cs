namespace Apollo.Domain.Voice;

/// <summary>
/// Within the open session, the current segment closes and a new one with <paramref name="Flags"/> opens.
/// </summary>
public sealed record VoiceSegmentChanged(Guid UserId, DateTimeOffset At, VoiceFlags Flags) : VoiceChange(UserId, At);
