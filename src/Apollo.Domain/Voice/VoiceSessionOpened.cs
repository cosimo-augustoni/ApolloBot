namespace Apollo.Domain.Voice;

/// <summary>
/// A session opens in <paramref name="ChannelId"/>, with a first segment carrying <paramref name="Flags"/>.
/// </summary>
public sealed record VoiceSessionOpened(Guid UserId, DateTimeOffset At, Guid ChannelId, VoiceFlags Flags) : VoiceChange(UserId, At);
