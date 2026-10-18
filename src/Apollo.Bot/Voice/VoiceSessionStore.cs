using System.Collections.Concurrent;
using Apollo.Bot.Status;
using Apollo.Domain.Voice;

namespace Apollo.Bot.Voice;

/// <summary>
/// The voice recorder's in-memory state, shared by its scoped instances: the <see cref="VoiceSessionStateMachine"/>,
/// the keys of every open session and segment row, so closing one is an update by primary key, and what the
/// reconciliation (#98) needs to know about the time the bot could not see. Callers hold <see cref="EnterAsync"/> while
/// they read or change it, and replace <see cref="Machine"/> only once the result is stored.
/// </summary>
internal sealed class VoiceSessionStore : IOpenVoiceSessionCounter, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ConcurrentDictionary<Guid, int> _pendingEvents = [];
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
    /// When this run started; T for the startup reconciliation when no heartbeat of a previous run exists, and for the
    /// first periodic run.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Whether the sessions a previous run left open in the database have been taken over. Nothing is applied before.
    /// </summary>
    public bool TakenOver { get; set; }

    /// <summary>
    /// The guild of the latest full guild sync, for the periodic run; <see langword="null"/> before the first one.
    /// </summary>
    public Guid? GuildId { get; set; }

    /// <summary>
    /// Whether the startup reconciliation (T = the previous run's last heartbeat) has run for everyone.
    /// </summary>
    public bool StartupReconciled { get; set; }

    /// <summary>
    /// The previous run's last connected heartbeat, read once for the startup reconciliation.
    /// </summary>
    public StartupHeartbeat? StartupHeartbeat { get; set; }

    /// <summary>
    /// When the latest full reconciliation ran: T for the next periodic run.
    /// </summary>
    public DateTimeOffset? LastReconciledAt { get; set; }

    /// <summary>
    /// When the gateway disconnected, until the next full reconciliation (or a resume, which replays what was missed):
    /// T after the reconnect.
    /// </summary>
    public DateTimeOffset? DisconnectedAt { get; set; }

    /// <summary>
    /// Whether the gateway is disconnected now; the periodic run waits for the reconnect.
    /// </summary>
    public bool GatewayDown { get; set; }

    public int CountOpenSessions() => _openSessions;

    /// <summary>
    /// Waits for exclusive access; dispose the result to release it. Releasing also refreshes the open session count.
    /// </summary>
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        return new Lease(this);
    }

    /// <summary>
    /// Marks a voice event of <paramref name="userId"/> as on its way to the state machine until the result is disposed,
    /// so a periodic run does not mistake the change it carries for one the bot missed.
    /// </summary>
    /// <remarks>The seam for an event buffer (#99): buffered events of a user should count here too.</remarks>
    public IDisposable BeginEvent(Guid userId)
    {
        _pendingEvents.AddOrUpdate(userId, 1, (_, count) => count + 1);
        return new PendingEvent(this, userId);
    }

    /// <summary>
    /// Whether a voice event of <paramref name="userId"/> is on its way (see <see cref="BeginEvent"/>).
    /// </summary>
    public bool HasPendingEvents(Guid userId) => _pendingEvents.ContainsKey(userId);

    public void Dispose() => _lock.Dispose();

    private void EndEvent(Guid userId)
    {
        if (_pendingEvents.AddOrUpdate(userId, 0, (_, count) => count - 1) <= 0)
        {
            _pendingEvents.TryRemove(new KeyValuePair<Guid, int>(userId, 0));
        }
    }

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

    private sealed class PendingEvent(VoiceSessionStore store, Guid userId) : IDisposable
    {
        private int _ended;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _ended, 1) == 0)
            {
                store.EndEvent(userId);
            }
        }
    }
}
