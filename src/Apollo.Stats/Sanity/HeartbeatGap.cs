namespace Apollo.Stats.Sanity;

/// <summary>Two consecutive heartbeats (<c>bot_status</c>, #24) further apart than the threshold.</summary>
/// <param name="Start">The heartbeat before the gap (UTC).</param>
/// <param name="End">The first heartbeat after it (UTC).</param>
public sealed record HeartbeatGap(DateTimeOffset Start, DateTimeOffset End)
{
    public TimeSpan Duration => End - Start;
}
