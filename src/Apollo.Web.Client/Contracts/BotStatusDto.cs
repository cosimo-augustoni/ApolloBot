using System.Text.Json.Serialization;

namespace Apollo.Web.Client.Contracts;

/// <summary>
/// <c>GET /api/admin/bot-status</c> (#31, #91): the bot's latest heartbeat (#24), what the database says about the
/// recorded data, and the events recorded per hour.
/// </summary>
/// <param name="CheckedAt">When the host answered (UTC); relative times on the page count from here.</param>
/// <param name="LastHeartbeatAt">The latest heartbeat (UTC); <see langword="null"/> when the bot never wrote one.</param>
/// <param name="Connected">Whether the latest heartbeat reported the gateway as connected.</param>
/// <param name="LastConnectedAt">The latest heartbeat that reported the gateway as connected; <see langword="null"/> when none ever did.</param>
/// <param name="GatewayLatencyMeanMs">
/// The mean gateway latency of the heartbeats in the last 5 minutes, rounded to whole ms; heartbeats without a latency
/// do not count, <see langword="null"/> when none is left.
/// </param>
/// <param name="LastRecordedEvent">The newest recorded row; <see langword="null"/> when nothing is recorded.</param>
/// <param name="OpenVoiceSessions">The voice sessions open now (tracked members only), oldest first.</param>
/// <param name="IsStale">The latest heartbeat is older than the stale threshold (default 2 minutes).</param>
/// <param name="NoTrackingRole">A synced platform has no tracking role set, so nobody is tracked there (#62).</param>
/// <param name="EventsPerHour">The last 24 <c>Europe/Zurich</c> hours, oldest first, the current hour last.</param>
public sealed record BotStatusDto(
    DateTimeOffset CheckedAt,
    DateTimeOffset? LastHeartbeatAt,
    bool Connected,
    DateTimeOffset? LastConnectedAt,
    int? GatewayLatencyMeanMs,
    LastRecordedEventDto? LastRecordedEvent,
    IReadOnlyList<OpenVoiceSessionDto> OpenVoiceSessions,
    bool IsStale,
    bool NoTrackingRole,
    IReadOnlyList<HourlyEventCountDto> EventsPerHour)
{
    /// <summary>Whether the bot has written a heartbeat at all; without one the other status fields mean nothing.</summary>
    [JsonIgnore]
    public bool HasHeartbeat => LastHeartbeatAt is not null;
}
