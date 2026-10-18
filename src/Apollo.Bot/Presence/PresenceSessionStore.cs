using Apollo.Bot.Reconciliation;
using Apollo.Domain.Presence;

namespace Apollo.Bot.Presence;

/// <summary>
/// The presence recorder's in-memory state, shared by its scoped instances: the <see cref="PresenceSessionStateMachine"/>
/// with every open session, the guild of each user who has one, and what the periodic reconciliation (#98) needs to
/// know. Callers hold <see cref="EnterAsync"/> while they read or change it, and replace <see cref="Machine"/> only once
/// the closed sessions are stored.
/// </summary>
internal sealed class PresenceSessionStore : IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly PendingEvents _pendingEvents = new();

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
    /// When this run started: T for the first periodic run.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// The guild of the latest full guild sync, for the periodic run; <see langword="null"/> before the first one.
    /// </summary>
    public Guid? GuildId { get; set; }

    /// <summary>
    /// When the latest full reconciliation (a guild sync or a periodic run) compared everyone with the current
    /// presences: T for the next periodic run.
    /// </summary>
    public DateTimeOffset? LastReconciledAt { get; set; }

    /// <summary>
    /// Whether the gateway is disconnected now; the periodic run waits for the reconnect.
    /// </summary>
    public bool GatewayDown { get; set; }

    /// <summary>
    /// Whether a disconnect has not been followed by a full guild sync yet; the periodic run waits for it, since the new
    /// session's cache may still be empty.
    /// </summary>
    public bool AwaitingSync { get; set; }

    /// <summary>
    /// Waits for exclusive access; dispose the result to release it.
    /// </summary>
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        return new Lease(_lock);
    }

    /// <summary>
    /// Marks a presence update of <paramref name="userId"/> as on its way until the result is disposed, so the periodic
    /// run leaves the member to it.
    /// </summary>
    public IDisposable BeginEvent(Guid userId) => _pendingEvents.Begin(userId);

    /// <summary>
    /// Whether a presence update of <paramref name="userId"/> is on its way (see <see cref="BeginEvent"/>).
    /// </summary>
    public bool HasPendingEvents(Guid userId) => _pendingEvents.Contains(userId);

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
