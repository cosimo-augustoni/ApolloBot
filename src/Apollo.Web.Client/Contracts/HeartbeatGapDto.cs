namespace Apollo.Web.Client.Contracts;

/// <summary>Two consecutive bot heartbeats further apart than the threshold (#40).</summary>
/// <param name="Start">The heartbeat before the gap (UTC).</param>
/// <param name="End">The first heartbeat after it (UTC).</param>
public sealed record HeartbeatGapDto(DateTimeOffset Start, DateTimeOffset End);
