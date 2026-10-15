namespace Apollo.Spikes.TimescaleEf;

/// <summary>
/// Stand-in for a raw event table: append-only, partitioned by <see cref="OccurredAt"/>.
/// </summary>
public sealed class ActivityEvent
{
    public long Id { get; set; }

    public DateTimeOffset OccurredAt { get; set; }

    public long ChannelId { get; set; }
}
