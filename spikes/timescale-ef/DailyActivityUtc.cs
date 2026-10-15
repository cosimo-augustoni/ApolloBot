namespace Apollo.Spikes.TimescaleEf;

/// <summary>
/// Daily aggregate defined purely with CmdScale's fluent API. Its buckets are UTC days.
/// </summary>
public sealed class DailyActivityUtc
{
    public DateTimeOffset Day { get; set; }

    public long ChannelId { get; set; }

    public long EventCount { get; set; }
}
