using Apollo.Platform.Abstractions;

namespace Apollo.Platform.Discord;

/// <summary>
/// <see cref="IPlatformConnectionStatus"/> fed by <see cref="DiscordConnectionHandler"/> from the gateway lifecycle and
/// NetCord's latency updates. Written on gateway threads, read by the heartbeat: the state is one immutable snapshot.
/// </summary>
internal sealed class DiscordConnectionStatus(DiscordEventMapper mapper, TimeProvider timeProvider) : IPlatformConnectionStatus
{
    private PlatformConnectionSnapshot _current = new(false, timeProvider.GetUtcNow(), null);

    public PlatformRef Guild => mapper.Guild;

    public PlatformConnectionSnapshot Current => Volatile.Read(ref _current);

    /// <summary>
    /// A new or resumed session: its latency is unknown until NetCord's first measurement.
    /// </summary>
    public void Connected() => Volatile.Write(ref _current, new PlatformConnectionSnapshot(true, timeProvider.GetUtcNow(), null));

    public void Disconnected() => Volatile.Write(ref _current, new PlatformConnectionSnapshot(false, timeProvider.GetUtcNow(), null));

    /// <summary>
    /// Keeps the latency only while connected; an update racing a disconnect must not bring back a stale value.
    /// </summary>
    public void LatencyUpdated(TimeSpan latency)
    {
        var current = Volatile.Read(ref _current);
        while (current.IsConnected)
        {
            var seen = Interlocked.CompareExchange(ref _current, current with { Latency = latency }, current);
            if (ReferenceEquals(seen, current))
            {
                return;
            }

            current = seen;
        }
    }
}
