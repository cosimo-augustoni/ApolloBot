namespace Apollo.Stats.Raw;

/// <summary>
/// A closed voice session, with its full duration (not clipped to the range).
/// </summary>
public sealed record LongestVoiceSession(Guid UserId, Guid ChannelId, DateTimeOffset StartedAt, TimeSpan Duration);
