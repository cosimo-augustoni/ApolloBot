using Apollo.Domain.Presence;

namespace Apollo.Bot.Presence;

/// <summary>
/// The presence recorder's in-memory state, shared by its scoped instances: the <see cref="PresenceSessionStateMachine"/>
/// with every open session, and the guild of each user who has one. Callers hold <see cref="EnterAsync"/> while they
/// read or change it, and replace <see cref="Machine"/> only once the closed sessions are stored.
/// </summary>
internal sealed class PresenceSessionStore : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// The state as stored. Changes are made on a <see cref="PresenceSessionStateMachine.Clone"/> that replaces this once
    /// its rows are committed, so a failed save leaves the sessions open, to be stored by a later close.
    /// </summary>
    public PresenceSessionStateMachine Machine { get; set; } = new();

    /// <summary>
    /// The internal guild ID per internal user ID with an open session, for the rows of their sessions.
    /// </summary>
    public Dictionary<Guid, Guid> Guilds { get; } = [];

    /// <summary>
    /// Set by the shutdown close: from then on nothing opens, although the gateway may still deliver events.
    /// </summary>
    public bool Stopped { get; set; }

    /// <summary>
    /// Waits for exclusive access; dispose the result to release it.
    /// </summary>
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        return new Lease(_lock);
    }

    public void Dispose() => _lock.Dispose();

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
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
