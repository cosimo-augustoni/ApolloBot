namespace Apollo.Bot.Names;

/// <summary>
/// Serialises the <see cref="MemberNameRecorder"/>'s reads and writes of <c>member_name_changes</c> within the process.
/// The gateway delivers events concurrently, and two writers comparing with the same latest row would both store it.
/// </summary>
internal sealed class MemberNameLock : IDisposable
{
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
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
