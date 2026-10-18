using Apollo.Bot.Reconciliation;
using Microsoft.Extensions.Options;

namespace Apollo.Bot.Presence;

/// <summary>
/// The periodic run and the shutdown of presence recording: every <see cref="ReconciliationOptions.Interval"/> it
/// reconciles the open sessions with the current presences (#98), so a close the bot failed to store ends at the
/// previous run instead of at the member's next presence update; on a graceful stop it closes every open session
/// (storing those of at least a minute) and opens nothing after. Nothing is left open in the database by a previous run,
/// so starting only records the start time; opening sessions for users already in an activity is left to the tracking
/// gate's first <c>TrackingStartedAsync</c> calls after the first guild sync.
/// </summary>
/// <remarks>A failed periodic run is logged and retried on the next tick.</remarks>
internal sealed partial class PresenceSessionLifecycleService(
    IServiceScopeFactory scopeFactory,
    IOptions<ReconciliationOptions> options,
    TimeProvider timeProvider,
    ILogger<PresenceSessionLifecycleService> logger) : BackgroundService
{
    private PeriodicTimer? _timer;

    /// <summary>
    /// Records the start and creates the timer before <see cref="ExecuteAsync"/> starts, so the first periodic run is due
    /// exactly one interval after the host started.
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new PeriodicTimer(options.Value.Interval, timeProvider);
        await using (var scope = scopeFactory.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<PresenceSessionRecorder>().StartAsync(cancellationToken);
        }

        await base.StartAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
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

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = _timer ?? throw new InvalidOperationException("The presence reconciliation was not started.");
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<PresenceSessionRecorder>().ReconcileAsync(stoppingToken);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException && stoppingToken.IsCancellationRequested))
            {
                LogReconciliationFailed(exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Closing the open presence sessions on shutdown failed; they are lost")]
    private partial void LogShutdownCloseFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The periodic presence reconciliation failed; retrying on the next tick")]
    private partial void LogReconciliationFailed(Exception exception);
}
