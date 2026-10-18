using Microsoft.Extensions.Options;

namespace Apollo.Bot.Voice;

/// <summary>
/// Startup and the periodic run of the voice reconciliation (#98): on start, takes over the sessions a previous run
/// left open; then reconciles every <see cref="VoiceReconciliationOptions.Interval"/>, so a change the bot failed to
/// store is corrected without a restart. A graceful stop leaves the open sessions open: the next start decides with the
/// rule whether they continue.
/// </summary>
/// <remarks>
/// A failed take-over is logged and retried before the first change is applied, and a failed periodic run on the next
/// tick, so a database hiccup does not stop the bot.
/// </remarks>
internal sealed partial class VoiceSessionLifecycleService(
    IServiceScopeFactory scopeFactory,
    IOptions<VoiceReconciliationOptions> options,
    TimeProvider timeProvider,
    ILogger<VoiceSessionLifecycleService> logger) : BackgroundService
{
    private PeriodicTimer? _timer;

    /// <summary>
    /// Takes over the open sessions and creates the timer before <see cref="ExecuteAsync"/> starts, so the first periodic
    /// run is due exactly one interval after the host started.
    /// </summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new PeriodicTimer(options.Value.Interval, timeProvider);
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<VoiceSessionRecorder>().StartAsync(cancellationToken);
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            LogTakeOverFailed(exception);
        }

        await base.StartAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = _timer ?? throw new InvalidOperationException("The voice reconciliation was not started.");
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<VoiceSessionRecorder>().ReconcileAsync(stoppingToken);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException && stoppingToken.IsCancellationRequested))
            {
                LogReconciliationFailed(exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Taking over the voice sessions left open by the previous run failed; retrying before the first change is stored")]
    private partial void LogTakeOverFailed(Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "The periodic voice reconciliation failed; retrying on the next tick")]
    private partial void LogReconciliationFailed(Exception exception);
}
