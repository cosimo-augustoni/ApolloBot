namespace Apollo.Bot.GuildState;

/// <summary>
/// Tracks the snapshot whose member pass may still be retrying. Starting a newer snapshot cancels the older run, so
/// retries of two snapshots never overlap (no duplicate downloads, no <c>left_at</c> from an older snapshot, Fluxer's
/// one request per 30 s). Every run also stops when the host stops, so a retry delay never holds the bot open.
/// </summary>
internal sealed class GuildSnapshotRuns(IHostApplicationLifetime lifetime) : IDisposable
{
    private readonly Lock _lock = new();
    private CancellationTokenSource? _current;

    /// <summary>
    /// Starts a run and cancels the previous one. The caller disposes the returned source when its run ends.
    /// </summary>
    public CancellationTokenSource Start(CancellationToken cancellationToken)
    {
        var run = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, lifetime.ApplicationStopping);
        CancellationTokenSource? previous;
        lock (_lock)
        {
            previous = _current;
            _current = run;
        }

        try
        {
            previous?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // The previous run already ended and disposed its source.
        }

        return run;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            try
            {
                _current?.Cancel();
            }
            catch (ObjectDisposedException)
            {
                // Already ended.
            }

            _current = null;
        }
    }
}
