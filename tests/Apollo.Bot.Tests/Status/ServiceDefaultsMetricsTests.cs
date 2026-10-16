using System.Diagnostics.Metrics;
using Apollo.Platform.Abstractions.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;

namespace Apollo.Bot.Tests.Status;

/// <summary>
/// ServiceDefaults can't reference Apollo.Platform.Abstractions, so it names the meter as a literal; this keeps the two
/// in sync.
/// </summary>
public sealed class ServiceDefaultsMetricsTests
{
    [Fact]
    public void AddServiceDefaults_ShouldExportTheMeterOfRecordedEventMetrics()
    {
        using var host = BuildHost();
        var meterFactory = host.Services.GetRequiredService<IMeterFactory>();

        var counter = meterFactory.Create(RecordedEventMetrics.MeterName).CreateCounter<long>(RecordedEventMetrics.RecordedCounter);

        counter.Enabled.ShouldBeTrue($"ServiceDefaults must call AddMeter(\"{RecordedEventMetrics.MeterName}\")");
    }

    [Fact]
    public void AddServiceDefaults_ShouldNotExportUnknownMeters()
    {
        using var host = BuildHost();
        var meterFactory = host.Services.GetRequiredService<IMeterFactory>();

        var counter = meterFactory.Create("Apollo.Unknown").CreateCounter<long>("apollo.unknown");

        counter.Enabled.ShouldBeFalse();
    }

    private static IHost BuildHost()
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });

        // As under Aspire: without an exporter the SDK has no reader and enables no instrument at all. Nothing is sent
        // before the host is disposed.
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://127.0.0.1:4317",
        });
        builder.AddServiceDefaults();
        var host = builder.Build();

        // The OpenTelemetry host service creates the provider on start; resolving it subscribes the meters without
        // starting the host.
        host.Services.GetRequiredService<MeterProvider>();
        return host;
    }
}
