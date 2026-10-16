using System.Net.WebSockets;
using Apollo.Platform.Abstractions;
using Microsoft.Extensions.Logging;
using NetCord.Gateway;
using NetCord.Hosting.Gateway;

namespace Apollo.Platform.Discord;

/// <summary>
/// Logs the gateway connection lifecycle and reports it as <see cref="Abstractions.Events.PlatformConnectionStateChanged"/>.
/// NetCord reconnects on its own after a disconnect: it either resumes the session (Resume) or starts a new one (Ready).
/// </summary>
/// <remarks>NetCord's gateway handlers carry no cancellation token, so dispatching is not cancellable.</remarks>
internal sealed partial class DiscordConnectionHandler(
    DiscordEventMapper mapper,
    TimeProvider timeProvider,
    ILogger<DiscordConnectionHandler> logger) : IReadyGatewayHandler, IResumeGatewayHandler, IDisconnectGatewayHandler
{
    public ValueTask HandleAsync(ReadyEventArgs arg)
    {
        LogConnected(PlatformConnectionState.Connected, mapper.Guild.ExternalId);
        return ReportAsync(PlatformConnectionState.Connected);
    }

    public ValueTask HandleAsync()
    {
        LogResumed(PlatformConnectionState.Resumed);
        return ReportAsync(PlatformConnectionState.Resumed);
    }

    public ValueTask HandleAsync(DisconnectEventArgs arg)
    {
        LogDisconnected(PlatformConnectionState.Disconnected, arg.CloseStatus, arg.Reconnect);
        return ReportAsync(PlatformConnectionState.Disconnected);
    }

    private ValueTask ReportAsync(PlatformConnectionState state) =>
        mapper.ForwardConnectionStateAsync(state, timeProvider.GetUtcNow(), CancellationToken.None);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discord gateway {ConnectionState}, recording guild {GuildId}")]
    private partial void LogConnected(PlatformConnectionState connectionState, string guildId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discord gateway {ConnectionState}, missed events were replayed")]
    private partial void LogResumed(PlatformConnectionState connectionState);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Discord gateway {ConnectionState} with close status {CloseStatus}, reconnect: {Reconnect}")]
    private partial void LogDisconnected(PlatformConnectionState connectionState, WebSocketCloseStatus? closeStatus, bool reconnect);
}
