using System.Globalization;

namespace Apollo.Spikes.TimescaleEf;

public sealed record SeedPoint(long ChannelId, DateTimeOffset OccurredAt)
{
    public SeedPoint(long channelId, string occurredAtUtc)
        : this(channelId, DateTimeOffset.Parse(occurredAtUtc, CultureInfo.InvariantCulture))
    {
    }
}
