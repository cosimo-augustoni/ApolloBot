namespace Apollo.Web.Client.Contracts;

/// <summary>A closed voice session, with its full duration (not clipped to the range).</summary>
/// <param name="Channel">The voice channel.</param>
/// <param name="StartedAt">When it started (UTC).</param>
/// <param name="DurationSeconds">How long it lasted.</param>
public sealed record LongestVoiceSessionDto(EventChannelDto Channel, DateTimeOffset StartedAt, long DurationSeconds);
