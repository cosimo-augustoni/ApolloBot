using System.Diagnostics.Metrics;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Platform.Abstractions.Diagnostics;

/// <summary>
/// The bot's event counters (OpenTelemetry meter <see cref="MeterName"/>, shown in the Aspire dashboard). Recorders call
/// <see cref="Recorded"/> after storing an event; the guild filter and the tracking gate call <see cref="Dropped"/>.
/// It lives here rather than in Apollo.Bot because the adapters' guild filter counts drops too.
/// </summary>
public sealed class RecordedEventMetrics
{
    public const string MeterName = "Apollo.Bot";

    public const string RecordedCounter = "apollo.events.recorded";

    public const string DroppedCounter = "apollo.events.dropped";

    private readonly Counter<long> _recorded;
    private readonly Counter<long> _dropped;

    public RecordedEventMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);
        var meter = meterFactory.Create(MeterName);
        _recorded = meter.CreateCounter<long>(RecordedCounter, unit: "{event}", description: "Events stored by a recorder.");
        _dropped = meter.CreateCounter<long>(DroppedCounter, unit: "{event}", description: "Events dropped before recording.");
    }

    /// <summary>
    /// Counts a stored event, tagged <c>event_type</c> with its runtime type name (e.g. <c>PlatformMessageCreated</c>).
    /// </summary>
    public void Recorded(PlatformEvent platformEvent)
    {
        ArgumentNullException.ThrowIfNull(platformEvent);
        _recorded.Add(1, new KeyValuePair<string, object?>("event_type", platformEvent.GetType().Name));
    }

    /// <summary>
    /// Counts a dropped event, tagged <c>reason</c> (<c>other_guild</c>, <c>untracked</c>).
    /// </summary>
    public void Dropped(EventDropReason reason) =>
        _dropped.Add(1, new KeyValuePair<string, object?>("reason", ToTag(reason)));

    private static string ToTag(EventDropReason reason) => reason switch
    {
        EventDropReason.OtherGuild => "other_guild",
        EventDropReason.Untracked => "untracked",
        _ => throw new ArgumentOutOfRangeException(nameof(reason), reason, null),
    };
}
