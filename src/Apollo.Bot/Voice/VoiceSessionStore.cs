using Apollo.Bot.Status;
using Apollo.Domain.Voice;

namespace Apollo.Bot.Voice;

/// <summary>
/// The voice recorder's in-memory state, shared by its scoped instances: the <see cref="VoiceSessionStateMachine"/>
/// and the keys of every open session and segment row, so closing one is an update by primary key. Callers hold
/// <see cref="EnterAsync"/> while they read or change it, and replace <see cref="Machine"/> only once the result is
/// stored.
/// </summary>
internal sealed class VoiceSessionStore : IOpenVoiceSessionCounter, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private volatile int _openSessions;

    /// <summary>
    /// The state as stored. Changes are made on a <see cref="VoiceSessionStateMachine.Clone"/> that replaces this once
    /// its rows are committed, so a failed save leaves state and rows in step.
    /// </summary>
    public VoiceSessionStateMachine Machine { get; set; } = new();

    /// <summary>
    /// The open rows per internal user ID.
    /// </summary>
    public Dictionary<Guid, OpenVoiceRows> Rows { get; } = [];

    /// <summary>
    /// Whether the sessions a previous run left open in the database have been closed. Nothing is opened before.
    /// </summary>
    public bool LeftOpenSessionsClosed { get; set; }

    /// <summary>
    /// Set by the shutdown close: from then on nothing opens, although the gateway may still deliver events.
    /// </summary>
    public bool Stopped { get; set; }

    public int CountOpenSessions() => _openSessions;

    /// <summary>
    /// Waits for exclusive access; dispose the result to release it. Releasing also refreshes the open session count.
    /// </summary>
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        return new Lease(this);
    }

    public void Dispose() => _lock.Dispose();

    private sealed class Lease(VoiceSessionStore store) : IDisposable
    {
        private int _released;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _released, 1) == 0)
            {
                store._openSessions = store.Machine.OpenSessionCount;
                store._lock.Release();
            }
        }
    }
}
