using Apollo.Bot.Status;
using Apollo.Platform.Abstractions;
using Apollo.Testing;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Bot.Tests.Status;

public sealed class GatewayConnectionHealthCheckTests
{
    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private readonly FakePlatformConnectionStatus _connection;

    public GatewayConnectionHealthCheckTests() => _connection = new FakePlatformConnectionStatus(_time);

    [Fact]
    public async Task Check_WhenConnected_ShouldBeHealthy()
    {
        _connection.Connect(TimeSpan.FromMilliseconds(50));
        _time.Advance(TimeSpan.FromHours(1));

        (await CheckAsync()).Status.ShouldBe(HealthStatus.Healthy);
    }

    [Fact]
    public async Task Check_WhenDisconnectedForExactlyTwoMinutes_ShouldBeDegraded()
    {
        _connection.Connect().Disconnect();
        _time.Advance(TimeSpan.FromMinutes(2));

        (await CheckAsync()).Status.ShouldBe(HealthStatus.Degraded);
    }

    [Fact]
    public async Task Check_WhenDisconnectedForMoreThanTwoMinutes_ShouldBeUnhealthy()
    {
        _connection.Connect().Disconnect();
        _time.Advance(TimeSpan.FromMinutes(2) + TimeSpan.FromSeconds(1));

        (await CheckAsync()).Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Check_WhenNeverConnectedForMoreThanTwoMinutes_ShouldBeUnhealthy()
    {
        _time.Advance(TimeSpan.FromMinutes(3));

        (await CheckAsync()).Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task HealthCheckService_WhenGatewayIsDisconnectedForMoreThanTwoMinutes_ShouldReportTheBotUnhealthy()
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<IPlatformConnectionStatus>(_connection)
            .AddBotStatus()
            .BuildServiceProvider();
        _time.Advance(TimeSpan.FromMinutes(5));

        var report = await services.GetRequiredService<HealthCheckService>().CheckHealthAsync(TestContext.Current.CancellationToken);

        report.Status.ShouldBe(HealthStatus.Unhealthy);
        report.Entries[GatewayConnectionHealthCheck.Name].Status.ShouldBe(HealthStatus.Unhealthy);
    }

    private Task<HealthCheckResult> CheckAsync() =>
        new GatewayConnectionHealthCheck(_connection, _time)
            .CheckHealthAsync(new HealthCheckContext(), TestContext.Current.CancellationToken);
}
