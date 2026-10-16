using System.Diagnostics.Metrics;

namespace Apollo.Testing.Diagnostics;

/// <summary>
/// An <see cref="IMeterFactory"/> whose meters are scoped to this instance, so a
/// <c>MetricCollector&lt;T&gt;(factory, meterName, instrumentName)</c> sees only the measurements of one test.
/// </summary>
public sealed class TestMeterFactory : IMeterFactory
{
    private readonly List<Meter> _meters = [];

    public Meter Create(MeterOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        var meter = new Meter(options.Name, options.Version, options.Tags, scope: this);
        lock (_meters)
        {
            _meters.Add(meter);
        }

        return meter;
    }

    public void Dispose()
    {
        lock (_meters)
        {
            _meters.ForEach(m => m.Dispose());
            _meters.Clear();
        }
    }
}
