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
    DiscordConnectionStatus status,
    TimeProvider timeProvider,
    ILogger<DiscordConnectionHandler> logger)
    : IReadyGatewayHandler, IResumeGatewayHandler, IDisconnectGatewayHandler, ILatencyUpdateGatewayHandler
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

    /// <summary>
    /// NetCord measured a new gateway round-trip time (heartbeat ack).
    /// </summary>
    public ValueTask HandleAsync(TimeSpan arg)
    {
        status.LatencyUpdated(arg);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Updates <see cref="DiscordConnectionStatus"/> before dispatching, so handlers of the event see the new state.
    /// </summary>
    private ValueTask ReportAsync(PlatformConnectionState state)
    {
        if (state == PlatformConnectionState.Disconnected)
        {
            status.Disconnected();
        }
        else
        {
            status.Connected();
        }

        return mapper.ForwardConnectionStateAsync(state, timeProvider.GetUtcNow(), CancellationToken.None);
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Discord gateway {ConnectionState}, recording guild {GuildId}")]
    private partial void LogConnected(PlatformConnectionState connectionState, string guildId);

    [LoggerMessage(Level = LogLevel.Information, Message = "Discord gateway {ConnectionState}, missed events were replayed")]
    private partial void LogResumed(PlatformConnectionState connectionState);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Discord gateway {ConnectionState} with close status {CloseStatus}, reconnect: {Reconnect}")]
    private partial void LogDisconnected(PlatformConnectionState connectionState, WebSocketCloseStatus? closeStatus, bool reconnect);
}
