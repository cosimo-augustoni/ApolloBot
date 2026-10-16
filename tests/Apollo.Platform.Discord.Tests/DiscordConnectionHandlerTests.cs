using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Events;
using Apollo.Platform.Discord.Tests.Fakes;
using Apollo.Testing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Options;
using NetCord.Gateway;

namespace Apollo.Platform.Discord.Tests;

public sealed class DiscordConnectionHandlerTests
{
    private static readonly PlatformRef _guild = new(PlatformKind.Discord, "111111111111111111");

    private readonly RecordingEventDispatcher _dispatcher = new();
    private readonly FakeLogger<DiscordConnectionHandler> _logger = new();
    private readonly DiscordConnectionHandler _handler;

    public DiscordConnectionHandlerTests()
    {
        var options = Options.Create(new DiscordPlatformOptions { Token = DiscordHost.Token, GuildId = DiscordHost.GuildId });
        var mapper = new DiscordEventMapper(options, _dispatcher, new FakeLogger<DiscordEventMapper>());
        _handler = new DiscordConnectionHandler(mapper, TestTime.CreateProvider(), _logger);
    }

    [Fact]
    public async Task Ready_WhenSessionStarts_ShouldDispatchConnectedForConfiguredGuild()
    {
        await _handler.HandleAsync(NetCordPayloads.Ready());

        _dispatcher.Events.ShouldHaveSingleItem().ShouldBe(
            new PlatformConnectionStateChanged(_guild, TestTime.Now, PlatformConnectionState.Connected));
    }

    [Fact]
    public async Task Disconnect_WhenFollowedByResume_ShouldDispatchEachTransition()
    {
        await _handler.HandleAsync(new DisconnectEventArgs(null, null, reconnect: true));
        await ((NetCord.Hosting.Gateway.IResumeGatewayHandler)_handler).HandleAsync();

        _dispatcher.Events.Cast<PlatformConnectionStateChanged>().Select(e => e.State)
            .ShouldBe([PlatformConnectionState.Disconnected, PlatformConnectionState.Resumed]);
    }

    [Fact]
    public async Task Disconnect_WhenFollowedByReconnect_ShouldDispatchEachTransition()
    {
        await _handler.HandleAsync(new DisconnectEventArgs(null, null, reconnect: true));
        await _handler.HandleAsync(NetCordPayloads.Ready());

        _dispatcher.Events.Cast<PlatformConnectionStateChanged>().Select(e => e.State)
            .ShouldBe([PlatformConnectionState.Disconnected, PlatformConnectionState.Connected]);
    }

    [Fact]
    public async Task Disconnect_WhenFollowedByResume_ShouldLogEachTransitionWithStructuredTemplate()
    {
        await _handler.HandleAsync(new DisconnectEventArgs(System.Net.WebSockets.WebSocketCloseStatus.EndpointUnavailable, "going away", reconnect: true));
        await ((NetCord.Hosting.Gateway.IResumeGatewayHandler)_handler).HandleAsync();

        var records = _logger.Collector.GetSnapshot();
        records.Select(r => r.Level).ShouldBe([LogLevel.Warning, LogLevel.Information]);
        records.Select(r => State(r, "ConnectionState")).ShouldBe(["Disconnected", "Resumed"]);
        records.ShouldAllBe(r => State(r, "{OriginalFormat}") != r.Message);
        State(records[0], "Reconnect").ShouldBe("True");
    }

    [Fact]
    public async Task Ready_WhenSessionStarts_ShouldLogConnectedWithStructuredTemplate()
    {
        await _handler.HandleAsync(NetCordPayloads.Ready());

        var record = _logger.Collector.GetSnapshot().ShouldHaveSingleItem();
        record.Level.ShouldBe(LogLevel.Information);
        State(record, "ConnectionState").ShouldBe("Connected");
    }

    private static string? State(FakeLogRecord record, string key) =>
        record.StructuredState.ShouldNotBeNull().SingleOrDefault(p => p.Key == key).Value;
}
