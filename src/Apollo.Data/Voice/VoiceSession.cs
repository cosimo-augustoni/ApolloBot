using Apollo.Domain.Voice;

namespace Apollo.Data.Voice;

/// <summary>
/// One continuous stay of a tracked user in one voice channel (scope §5.1, #16). Inserted when the session opens; the
/// only update ever made sets <see cref="EndedAt"/> and <see cref="EndReason"/> when it closes.
/// </summary>
public sealed class VoiceSession
{
    public long Id { get; init; }

    public Guid GuildId { get; init; }

    /// <summary>
    /// When the session started (<c>started_at</c> in scope §5.1); the hypertable's partition column.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    public Guid UserId { get; init; }

    public Guid ChannelId { get; init; }

    /// <summary>
    /// When the session ended; <see langword="null"/> while it is open.
    /// </summary>
    public DateTimeOffset? EndedAt { get; init; }

    /// <summary>
    /// Why the session ended; <see langword="null"/> while it is open.
    /// </summary>
    public VoiceSessionEndReason? EndReason { get; init; }
}
