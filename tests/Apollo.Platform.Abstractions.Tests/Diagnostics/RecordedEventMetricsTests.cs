using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Testing.Diagnostics;
using Apollo.Testing.Platform;
using Microsoft.Extensions.Diagnostics.Metrics.Testing;

namespace Apollo.Platform.Abstractions.Tests.Diagnostics;

public sealed class RecordedEventMetricsTests : IDisposable
{
    private readonly TestMeterFactory _meterFactory = new();
    private readonly RecordedEventMetrics _metrics;

    public RecordedEventMetricsTests() => _metrics = new RecordedEventMetrics(_meterFactory);

    [Fact]
    public void Recorded_WhenARecorderStoresAnEvent_ShouldIncrementRecordedWithItsEventType()
    {
        using var recorded = Collect(RecordedEventMetrics.RecordedCounter);

        _metrics.Recorded(new PlatformMessageCreatedBuilder().Build());

        var measurement = recorded.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags.ShouldBe([new KeyValuePair<string, object?>("event_type", "PlatformMessageCreated")]);
    }

    [Theory]
    [InlineData(EventDropReason.OtherGuild, "other_guild")]
    [InlineData(EventDropReason.Untracked, "untracked")]
    public void Dropped_WhenAnEventIsDropped_ShouldIncrementDroppedWithItsReason(EventDropReason reason, string tag)
    {
        using var dropped = Collect(RecordedEventMetrics.DroppedCounter);

        _metrics.Dropped(reason);

        var measurement = dropped.GetMeasurementSnapshot().ShouldHaveSingleItem();
        measurement.Value.ShouldBe(1);
        measurement.Tags.ShouldBe([new KeyValuePair<string, object?>("reason", tag)]);
    }

    [Fact]
    public void Meter_ShouldBeNamedAfterTheBot()
    {
        RecordedEventMetrics.MeterName.ShouldBe("Apollo.Bot");
    }

    public void Dispose() => _meterFactory.Dispose();

    private MetricCollector<long> Collect(string instrument) =>
        new(_meterFactory, RecordedEventMetrics.MeterName, instrument);
}
