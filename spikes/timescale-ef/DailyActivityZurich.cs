namespace Apollo.Spikes.TimescaleEf;

/// <summary>
/// Daily aggregate bucketed by Europe/Zurich calendar day. <see cref="Day"/> is the Zurich midnight that starts the day, as a UTC instant.
/// The view is created in the migration through CmdScale's <c>CreateContinuousAggregate(viewDefinition: ...)</c>.
/// </summary>
public sealed class DailyActivityZurich
{
    public DateTimeOffset Day { get; set; }

    public long ChannelId { get; set; }

    public long EventCount { get; set; }
}
