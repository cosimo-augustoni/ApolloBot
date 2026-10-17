namespace Apollo.Bot.Voice;

/// <summary>
/// The primary keys of a user's open <c>voice_sessions</c> and <c>voice_state_segments</c> rows.
/// </summary>
internal sealed record OpenVoiceRows(
    Guid GuildId,
    long SessionId,
    DateTimeOffset SessionStartedAt,
    long SegmentId,
    DateTimeOffset SegmentStartedAt);
