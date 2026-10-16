namespace Apollo.Data.Status;

/// <summary>
/// One bot heartbeat (#24), appended every 30 s. Rows are never updated, so gaps in the heartbeat stay visible.
/// </summary>
public sealed class BotStatus
{
    public long Id { get; init; }

    /// <summary>
    /// The internal guild the bot records.
    /// </summary>
    public Guid GuildId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public bool Connected { get; init; }

    /// <summary>
    /// The gateway round-trip time; <see langword="null"/> while disconnected and right after a (re)connect, until the
    /// new session's first measurement.
    /// </summary>
    public int? GatewayLatencyMs { get; init; }

    /// <summary>
    /// When the bot last received a guild event (connection lifecycle events do not count); <see langword="null"/>
    /// before the first one since it started.
    /// </summary>
    public DateTimeOffset? LastEventAt { get; init; }

    public int OpenVoiceSessions { get; init; }
}
