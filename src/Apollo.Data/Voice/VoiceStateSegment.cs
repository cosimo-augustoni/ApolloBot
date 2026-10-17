namespace Apollo.Data.Voice;

/// <summary>
/// A stretch of a <see cref="VoiceSession"/> with unchanged mute/deaf/stream/video flags (scope §5.1, #16). Inserted
/// when it opens; the only update ever made sets <see cref="EndedAt"/> when it closes.
/// </summary>
public sealed class VoiceStateSegment
{
    public long Id { get; init; }

    public Guid GuildId { get; init; }

    /// <summary>
    /// When the segment started (<c>from</c> in scope §5.1); the hypertable's partition column.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    /// <summary>
    /// The <see cref="VoiceSession.Id"/> of the session the segment belongs to.
    /// </summary>
    public long SessionId { get; init; }

    /// <summary>
    /// The session's user, so per-user queries and deletes need no join between the two hypertables.
    /// </summary>
    public Guid UserId { get; init; }

    /// <summary>
    /// When the segment ended (<c>to</c> in scope §5.1); <see langword="null"/> while it is open.
    /// </summary>
    public DateTimeOffset? EndedAt { get; init; }

    public bool SelfMute { get; init; }

    public bool SelfDeaf { get; init; }

    public bool Streaming { get; init; }

    public bool Video { get; init; }
}
