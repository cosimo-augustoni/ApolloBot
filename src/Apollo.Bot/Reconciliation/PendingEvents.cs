using System.Collections.Concurrent;

namespace Apollo.Bot.Reconciliation;

/// <summary>
/// The users whose platform event is on its way to a recorder's state machine, so a periodic run does not mistake the
/// change it carries for one the bot missed (#98). The seam for an event buffer (#99): buffered events of a user should
/// count here too.
/// </summary>
internal sealed class PendingEvents
{
    private readonly ConcurrentDictionary<Guid, int> _counts = [];

    /// <summary>
    /// Marks an event of <paramref name="userId"/> as on its way until the result is disposed.
    /// </summary>
    public IDisposable Begin(Guid userId)
    {
        _counts.AddOrUpdate(userId, 1, (_, count) => count + 1);
        return new Pending(this, userId);
    }

    public bool Contains(Guid userId) => _counts.ContainsKey(userId);

    private void End(Guid userId)
    {
        if (_counts.AddOrUpdate(userId, 0, (_, count) => count - 1) <= 0)
        {
            _counts.TryRemove(new KeyValuePair<Guid, int>(userId, 0));
        }
    }

    private sealed class Pending(PendingEvents events, Guid userId) : IDisposable
    {
        private int _ended;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _ended, 1) == 0)
            {
                events.End(userId);
            }
        }
    }
}
