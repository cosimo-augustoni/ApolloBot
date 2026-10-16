namespace Apollo.Bot.GuildState;

/// <summary>
/// Serialises <see cref="GuildStateSync"/> runs within the process. The gateway delivers events concurrently, and a
/// member update must not interleave with a reconciliation that reads and replaces the same rows.
/// </summary>
internal sealed class GuildStateSyncLock : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> AcquireAsync(CancellationToken cancellationToken)
    {
        await _semaphore.WaitAsync(cancellationToken);
        return new Releaser(_semaphore);
    }

    public void Dispose() => _semaphore.Dispose();

    private sealed class Releaser(SemaphoreSlim semaphore) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                semaphore.Release();
            }
        }
    }
}
