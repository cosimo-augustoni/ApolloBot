using Apollo.Bot.Status;
using Apollo.Testing;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Apollo.Bot.Tests.Status;

public sealed class HealthStatusLogPublisherTests
{
    private readonly FakeLogger<HealthStatusLogPublisher> _logger = new();
    private readonly HealthStatusLogPublisher _publisher;

    public HealthStatusLogPublisherTests() => _publisher = new HealthStatusLogPublisher(_logger);

    [Theory]
    [InlineData(HealthStatus.Healthy)]
    [InlineData(HealthStatus.Degraded)]
    public async Task Publish_WhenTheFirstReportIsNotUnhealthy_ShouldLogTheInitialStatusAsInformation(HealthStatus status)
    {
        await PublishAsync(status, "Gateway disconnected since …");

        var record = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Information);
        State(record, "Check").ShouldBe(GatewayConnectionHealthCheck.Name);
        State(record, "Status").ShouldBe(status.ToString());
        State(record, "PreviousStatus").ShouldBeNull();
        record.Message.ShouldNotContain("changed");
    }

    [Fact]
    public async Task Publish_WhenTheFirstReportIsUnhealthy_ShouldLogTheInitialStatusAsError()
    {
        await PublishAsync(HealthStatus.Unhealthy);

        _logger.Collector.GetSnapshot().ShouldHaveSingleItem().Level.ShouldBe(LogLevel.Error);
    }

    [Fact]
    public async Task Publish_WhenACheckTurnsUnhealthy_ShouldLogTheTransitionAsError()
    {
        await PublishAsync(HealthStatus.Healthy);

        await PublishAsync(HealthStatus.Unhealthy, "Gateway disconnected since …");

        var record = _logger.Collector.LatestRecord;
        record.Level.ShouldBe(LogLevel.Error);
        State(record, "Check").ShouldBe(GatewayConnectionHealthCheck.Name);
        State(record, "PreviousStatus").ShouldBe("Healthy");
        State(record, "Status").ShouldBe("Unhealthy");
        State(record, "Description").ShouldBe("Gateway disconnected since …");
        _logger.Collector.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Publish_WhenTheStatusStaysTheSame_ShouldLogOnlyTheInitialStatus()
    {
        await PublishAsync(HealthStatus.Degraded);
        await PublishAsync(HealthStatus.Degraded);
        await PublishAsync(HealthStatus.Degraded);

        _logger.Collector.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Publish_WhenDegradedAtStartThenConnected_ShouldLogNoWarning()
    {
        await PublishAsync(HealthStatus.Degraded);

        await PublishAsync(HealthStatus.Healthy);

        _logger.Collector.GetSnapshot().ShouldAllBe(r => r.Level == LogLevel.Information);
        State(_logger.Collector.LatestRecord, "PreviousStatus").ShouldBe("Degraded");
    }

    [Fact]
    public async Task Publish_WhenACheckRecovers_ShouldLogTheTransitionAsInformation()
    {
        await PublishAsync(HealthStatus.Unhealthy);

        await PublishAsync(HealthStatus.Healthy);

        _logger.Collector.LatestRecord.Level.ShouldBe(LogLevel.Information);
        State(_logger.Collector.LatestRecord, "Status").ShouldBe("Healthy");
        _logger.Collector.Count.ShouldBe(2);
    }

    [Fact]
    public void AddBotStatus_ShouldRegisterThePublisher()
    {
        var time = TestTime.CreateProvider();
        using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(time)
            .AddSingleton<Apollo.Platform.Abstractions.IPlatformConnectionStatus>(new FakePlatformConnectionStatus(time))
            .AddBotStatus()
            .BuildServiceProvider();

        services.GetServices<IHealthCheckPublisher>().ShouldContain(p => p is HealthStatusLogPublisher);
    }

    [Fact]
    public async Task HealthCheckPublisher_WhenGatewayIsDisconnectedForMoreThanTwoMinutes_ShouldLogUnhealthy()
    {
        var time = TestTime.CreateProvider();
        await using var services = new ServiceCollection()
            .AddFakeLogging()
            .AddSingleton<TimeProvider>(time)
            .AddSingleton<Apollo.Platform.Abstractions.IPlatformConnectionStatus>(new FakePlatformConnectionStatus(time))
            .AddSingleton<IOpenVoiceSessionCounter>(new FixedOpenVoiceSessionCounter(0))
            .AddBotStatus()
            .Configure<HealthCheckPublisherOptions>(o => o.Delay = TimeSpan.Zero)
            .BuildServiceProvider();
        time.Advance(TimeSpan.FromMinutes(3));
        var publisherService = services.GetServices<IHostedService>()
            .Single(s => s.GetType().Name == "HealthCheckPublisherHostedService");
        var logs = services.GetFakeLogCollector();

        await publisherService.StartAsync(TestContext.Current.CancellationToken);
        var waited = System.Diagnostics.Stopwatch.StartNew();
        while (!logs.GetSnapshot().Any(r => r.Category == typeof(HealthStatusLogPublisher).FullName))
        {
            waited.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10), "the health check publisher did not run");
            await Task.Delay(TimeSpan.FromMilliseconds(50), TestContext.Current.CancellationToken);
        }

        await publisherService.StopAsync(TestContext.Current.CancellationToken);
        var record = logs.GetSnapshot().First(r => r.Category == typeof(HealthStatusLogPublisher).FullName);
        record.Level.ShouldBe(LogLevel.Error);
        State(record, "Check").ShouldBe(GatewayConnectionHealthCheck.Name);
    }

    private Task PublishAsync(HealthStatus status, string? description = null) =>
        _publisher.PublishAsync(
            new HealthReport(
                new Dictionary<string, HealthReportEntry>
                {
                    [GatewayConnectionHealthCheck.Name] = new(status, description, TimeSpan.Zero, null, null),
                },
                TimeSpan.Zero),
            TestContext.Current.CancellationToken);

    private static string? State(FakeLogRecord record, string key) =>
        record.StructuredState.ShouldNotBeNull().SingleOrDefault(p => p.Key == key).Value;
}
