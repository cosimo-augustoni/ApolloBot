using Apollo.Platform.Abstractions;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using Apollo.Testing.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordConnectionStatusTests : IDisposable
{
    private static readonly TimeSpan _latency = TimeSpan.FromMilliseconds(42);

    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private readonly TestMeterFactory _meterFactory = new();
    private readonly DiscordConnectionStatus _status;
    private readonly DiscordConnectionHandler _handler;

    public DiscordConnectionStatusTests()
    {
        var options = Options.Create(new DiscordPlatformOptions { Token = DiscordHost.Token, GuildId = DiscordHost.GuildId });
        var mapper = new DiscordEventMapper(options, new RecordingEventDispatcher(), new(_meterFactory), new FakeLogger<DiscordEventMapper>());
        _status = new DiscordConnectionStatus(mapper, _time);
        _handler = new DiscordConnectionHandler(mapper, _status, _time, new FakeLogger<DiscordConnectionHandler>());
    }

    [Fact]
    public void Status_BeforeTheFirstConnect_ShouldBeDisconnectedSinceStartWithoutLatency()
    {
        _time.Advance(TimeSpan.FromMinutes(1));

        _status.Current.IsConnected.ShouldBeFalse();
        _status.Current.StateChangedAt.ShouldBe(TestTime.Now);
        _status.Current.Latency.ShouldBeNull();
        _status.Guild.ShouldBe(new PlatformRef(PlatformKind.Discord, "111111111111111111"));
    }

    [Fact]
    public async Task Ready_WhenSessionStarts_ShouldBeConnectedSinceNow()
    {
        _time.Advance(TimeSpan.FromSeconds(5));

        await _handler.HandleAsync(NetCordPayloads.Ready());

        _status.Current.IsConnected.ShouldBeTrue();
        _status.Current.StateChangedAt.ShouldBe(TestTime.Now.AddSeconds(5));
    }

    [Fact]
    public async Task LatencyUpdate_WhenConnected_ShouldReportTheLatency()
    {
        await _handler.HandleAsync(NetCordPayloads.Ready());

        await _handler.HandleAsync(_latency);

        _status.Current.Latency.ShouldBe(_latency);
    }

    [Fact]
    public async Task Disconnect_WhenConnected_ShouldBeDisconnectedSinceNowWithoutLatency()
    {
        await _handler.HandleAsync(NetCordPayloads.Ready());
        await _handler.HandleAsync(_latency);
        _time.Advance(TimeSpan.FromMinutes(1));

        await _handler.HandleAsync(new DisconnectEventArgs(null, null, reconnect: true));

        _status.Current.IsConnected.ShouldBeFalse();
        _status.Current.StateChangedAt.ShouldBe(TestTime.Now.AddMinutes(1));
        _status.Current.Latency.ShouldBeNull();
    }

    [Fact]
    public async Task Resume_AfterADisconnect_ShouldReportNoLatencyUntilTheNewSessionMeasuresOne()
    {
        await _handler.HandleAsync(NetCordPayloads.Ready());
        await _handler.HandleAsync(_latency);
        await _handler.HandleAsync(new DisconnectEventArgs(null, null, reconnect: true));

        await ((IResumeGatewayHandler)_handler).HandleAsync();
        var beforeFirstMeasurement = _status.Current;
        await _handler.HandleAsync(TimeSpan.FromMilliseconds(55));

        beforeFirstMeasurement.Latency.ShouldBeNull();
        _status.Current.Latency.ShouldBe(TimeSpan.FromMilliseconds(55));
    }

    [Fact]
    public async Task LatencyUpdate_WhenDisconnected_ShouldBeIgnored()
    {
        await _handler.HandleAsync(_latency);

        _status.Current.Latency.ShouldBeNull();
    }

    [Fact]
    public async Task Resume_WhenDisconnected_ShouldBeConnectedAgain()
    {
        await _handler.HandleAsync(new DisconnectEventArgs(null, null, reconnect: true));

        await ((IResumeGatewayHandler)_handler).HandleAsync();

        _status.Current.IsConnected.ShouldBeTrue();
    }

    [Fact]
    public void AddDiscordPlatform_ShouldExposeTheStatusTheConnectionHandlerUpdates()
    {
        using var host = DiscordHost.Build(DiscordHost.ValidSettings());

        host.Services.GetRequiredService<IPlatformConnectionStatus>()
            .ShouldBeSameAs(host.Services.GetRequiredService<DiscordConnectionStatus>());
    }

    public void Dispose() => _meterFactory.Dispose();
}
