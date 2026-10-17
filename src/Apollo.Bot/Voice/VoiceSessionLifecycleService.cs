namespace Apollo.Bot.Voice;

/// <summary>
/// Startup and shutdown of voice recording (#16): on start, closes the sessions a previous run left open as
/// <c>bot_restart</c>; on a graceful stop, closes every open session as <c>bot_restart</c>. Opening sessions for users
/// already in voice is left to the tracking gate's first <c>TrackingStartedAsync</c> calls after the first guild sync.
/// </summary>
/// <remarks>
/// A failed close on startup is logged and retried before the first session opens, so a database hiccup does not stop
/// the bot.
/// </remarks>
internal sealed partial class VoiceSessionLifecycleService(
    IServiceScopeFactory scopeFactory,
    ILogger<VoiceSessionLifecycleService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<VoiceSessionRecorder>().CloseLeftOpenSessionsAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogStartupCloseFailed(exception);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<VoiceSessionRecorder>().CloseAllAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            LogShutdownCloseFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Closing the voice sessions left open by the previous run failed; retrying before the first session opens")]
    private partial void LogStartupCloseFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Closing the open voice sessions on shutdown failed; the next start closes them as bot_restart")]
    private partial void LogShutdownCloseFailed(Exception exception);
}
