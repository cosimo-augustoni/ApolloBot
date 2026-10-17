namespace Apollo.Web.Client.Contracts;

/// <summary>A <c>voice_sessions</c> row (scope §5.1, #16); both values are <see langword="null"/> while it is open.</summary>
/// <param name="EndedAt">When the session ended (UTC).</param>
/// <param name="EndReason">As stored: <c>Left</c>, <c>Moved</c>, <c>TrackingStopped</c> or <c>BotRestart</c>.</param>
public sealed record VoiceSessionDetailsDto(DateTimeOffset? EndedAt, string? EndReason);
