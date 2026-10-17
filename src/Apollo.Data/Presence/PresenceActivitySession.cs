using Apollo.Domain.Presence;

namespace Apollo.Data.Presence;

/// <summary>
/// One continuous activity of a tracked user, e.g. playing "Factorio" (scope §5.1, #19). Inserted once the session
/// closed, and only if it lasted at least a minute, so the table is strictly append-only; open sessions live only in the
/// recorder's memory.
/// </summary>
public sealed class PresenceActivitySession
{
    public long Id { get; init; }

    public Guid GuildId { get; init; }

    /// <summary>
    /// When the session started (<c>started_at</c> in scope §5.1); the hypertable's partition column.
    /// </summary>
    public DateTimeOffset OccurredAt { get; init; }

    public Guid UserId { get; init; }

    public PresenceActivityType ActivityType { get; init; }

    /// <summary>
    /// The activity name as the platform reports it, e.g. the game title.
    /// </summary>
    public required string ActivityName { get; init; }

    /// <summary>
    /// When the session ended.
    /// </summary>
    public DateTimeOffset EndedAt { get; init; }
}
