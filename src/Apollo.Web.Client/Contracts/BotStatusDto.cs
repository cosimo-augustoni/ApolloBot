using System.Text.Json.Serialization;

namespace Apollo.Web.Client.Contracts;

/// <summary>
/// <c>GET /api/admin/bot-status</c> (#31): the bot's latest heartbeat (#24) and the events recorded per hour.
/// </summary>
/// <param name="CheckedAt">When the host answered (UTC); relative times on the page count from here.</param>
/// <param name="LastHeartbeatAt">The latest heartbeat (UTC); <see langword="null"/> when the bot never wrote one.</param>
/// <param name="Connected">Whether the latest heartbeat reported the gateway as connected.</param>
/// <param name="GatewayLatencyMs">The latest gateway latency; <see langword="null"/> when unknown.</param>
/// <param name="LastEventAt">When the bot last received a guild event; <see langword="null"/> before the first since its start.</param>
/// <param name="OpenVoiceSessions">Voice sessions open at the latest heartbeat.</param>
/// <param name="IsStale">The latest heartbeat is older than the stale threshold (default 2 minutes).</param>
/// <param name="EventsPerHour">The last 24 <c>Europe/Zurich</c> hours, oldest first, the current hour last.</param>
public sealed record BotStatusDto(
    DateTimeOffset CheckedAt,
    DateTimeOffset? LastHeartbeatAt,
    bool Connected,
    int? GatewayLatencyMs,
    DateTimeOffset? LastEventAt,
    int OpenVoiceSessions,
    bool IsStale,
    IReadOnlyList<HourlyEventCountDto> EventsPerHour)
{
    /// <summary>Whether the bot has written a heartbeat at all; without one the other status fields mean nothing.</summary>
    [JsonIgnore]
    public bool HasHeartbeat => LastHeartbeatAt is not null;
}
