namespace Apollo.Bot.Tracking;

/// <summary>
/// Reloads the <see cref="TrackingGate"/> every <see cref="Interval"/>, so role setting changes (#33, or rows inserted
/// by hand in M1) apply without a restart (scope §5.1.2). Does nothing before the first guild sync. A failed reload is
/// logged and the next tick tries again, also when it was the reload right after a sync and no snapshot exists yet.
/// </summary>
internal sealed partial class TrackingGateReloader(TrackingGate gate, TimeProvider timeProvider, ILogger<TrackingGateReloader> logger)
    : BackgroundService
{
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    private PeriodicTimer? _timer;

    /// <summary>
    /// Creates the timer before <see cref="ExecuteAsync"/> starts, so the first reload is due exactly one
    /// <see cref="Interval"/> after the host started.
    /// </summary>
    public override Task StartAsync(CancellationToken cancellationToken)
    {
        _timer = new PeriodicTimer(Interval, timeProvider);
        return base.StartAsync(cancellationToken);
    }

    public override void Dispose()
    {
        _timer?.Dispose();
        base.Dispose();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var timer = _timer ?? throw new InvalidOperationException("The reloader was not started.");
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await gate.ReloadAsync(stoppingToken);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException && stoppingToken.IsCancellationRequested))
            {
                LogReloadFailed(exception);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to reload the tracking gate; retrying on the next tick")]
    private partial void LogReloadFailed(Exception exception);
}
