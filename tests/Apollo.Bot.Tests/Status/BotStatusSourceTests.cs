using Apollo.Bot.Status;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Testing;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Bot.Tests.Status;

public sealed class BotStatusSourceTests : IDisposable
{
    private static readonly TimeSpan _latency = TimeSpan.FromMilliseconds(87);

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private readonly FakePlatformConnectionStatus _connection;
    private ServiceProvider? _services;

    public BotStatusSourceTests() => _connection = new FakePlatformConnectionStatus(_time);

    [Fact]
    public void GetCurrent_WhenGatewayIsConnectedWithKnownLatency_ShouldReportConnectedAndThatLatency()
    {
        _connection.Connect(_latency);

        var status = Build().GetRequiredService<IBotStatusSource>().GetCurrent();

        status.Connected.ShouldBeTrue();
        status.GatewayLatency.ShouldBe(_latency);
        status.Guild.ShouldBe(TestPlatform.Guild);
    }

    [Fact]
    public void GetCurrent_WhenGatewayIsDisconnected_ShouldReportDisconnectedWithoutLatency()
    {
        _connection.Connect(_latency).Disconnect();

        var status = Build().GetRequiredService<IBotStatusSource>().GetCurrent();

        status.Connected.ShouldBeFalse();
        status.GatewayLatency.ShouldBeNull();
    }

    [Fact]
    public async Task GetCurrent_WhenAnEventWasDispatchedAtTAndNoneSince_ShouldReportLastEventAtT()
    {
        var services = Build();
        var platform = new FakeChatPlatform(services.GetRequiredService<IPlatformEventDispatcher>(), _time);
        var dispatchedAt = _time.GetUtcNow();

        await platform.RaiseAsync(new PlatformMessageCreatedBuilder().Build(), TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(3));

        services.GetRequiredService<IBotStatusSource>().GetCurrent().LastEventAt.ShouldBe(dispatchedAt);
    }

    [Fact]
    public void GetCurrent_WhenNoEventWasDispatched_ShouldReportNoLastEvent()
    {
        Build().GetRequiredService<IBotStatusSource>().GetCurrent().LastEventAt.ShouldBeNull();
    }

    [Fact]
    public void GetCurrent_WhenAVoiceSessionCounterReportsN_ShouldReportN()
    {
        var services = Build(s => s.AddSingleton<IOpenVoiceSessionCounter>(new FixedOpenVoiceSessionCounter(4)));

        services.GetRequiredService<IBotStatusSource>().GetCurrent().OpenVoiceSessions.ShouldBe(4);
    }

    [Fact]
    public void GetCurrent_WhenNoVoiceRecorderIsRegistered_ShouldReportZeroOpenVoiceSessions()
    {
        Build().GetRequiredService<IBotStatusSource>().GetCurrent().OpenVoiceSessions.ShouldBe(0);
    }

    public void Dispose() => _services?.Dispose();

    private ServiceProvider Build(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddSingleton<TimeProvider>(_time)
            .AddSingleton<IPlatformConnectionStatus>(_connection)
            .AddPlatformEventDispatcher();
        configure?.Invoke(services);
        services.AddBotStatus();
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        return _services;
    }

    private sealed class FixedOpenVoiceSessionCounter(int count) : IOpenVoiceSessionCounter
    {
        public int CountOpenSessions() => count;
    }
}
