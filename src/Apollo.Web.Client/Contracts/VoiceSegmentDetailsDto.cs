namespace Apollo.Web.Client.Contracts;

/// <summary>A <c>voice_state_segments</c> row (scope §5.1, #16): a stretch of a session with unchanged flags.</summary>
/// <param name="SessionId">The <see cref="RawEventDto.Id"/> of its voice session.</param>
/// <param name="EndedAt">When the segment ended (UTC); <see langword="null"/> while it is open.</param>
/// <param name="SelfMute">Self muted.</param>
/// <param name="SelfDeaf">Self deafened.</param>
/// <param name="Streaming">Streaming.</param>
/// <param name="Video">Camera on.</param>
public sealed record VoiceSegmentDetailsDto(long SessionId, DateTimeOffset? EndedAt, bool SelfMute, bool SelfDeaf, bool Streaming, bool Video);
