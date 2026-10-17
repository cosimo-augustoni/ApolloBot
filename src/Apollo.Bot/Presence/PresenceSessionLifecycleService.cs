namespace Apollo.Bot.Presence;

/// <summary>
/// Shutdown of presence recording (#19): on a graceful stop, closes every open session (storing those of at least a
/// minute) and opens nothing after. Nothing is left open in the database by a previous run, so starting needs nothing;
/// opening sessions for users already in an activity is left to the tracking gate's first <c>TrackingStartedAsync</c>
/// calls after the first guild sync.
/// </summary>
internal sealed partial class PresenceSessionLifecycleService(
    IServiceScopeFactory scopeFactory,
    ILogger<PresenceSessionLifecycleService> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<PresenceSessionRecorder>().CloseAllAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            LogShutdownCloseFailed(exception);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Closing the open presence sessions on shutdown failed; they are lost")]
    private partial void LogShutdownCloseFailed(Exception exception);
}
