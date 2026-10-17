namespace Apollo.Domain.Presence;

/// <summary>
/// Turns presence updates into activity sessions (scope §5.1, #19): the diff of a user's old and new activity sets. An
/// activity that appears opens a session, one that disappears closes it, so a switch from one game to another closes
/// and opens at the same instant, and simultaneous activities are independent sessions. A session is identified by
/// user, activity type and name. It holds tracked users only; the caller drops untracked users and custom statuses
/// before calling it, and passes no activities for a user who went offline. Not thread-safe: the caller serialises calls.
/// </summary>
public sealed class PresenceSessionStateMachine
{
    /// <summary>
    /// The shortest session that is stored (scope §5.1 notes, D1).
    /// </summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(1);

    // Per user, the start of every open session by its activity.
    private readonly Dictionary<Guid, Dictionary<PresenceActivity, DateTimeOffset>> _sessions = [];

    public int OpenSessionCount => _sessions.Values.Sum(sessions => sessions.Count);

    public IReadOnlyCollection<Guid> Users => [.. _sessions.Keys];

    /// <summary>
    /// The user's activities are now <paramref name="activities"/> (a presence update, or the current presence when
    /// tracking starts or after a reconnect).
    /// </summary>
    public IReadOnlyList<PresenceChange> Update(Guid userId, IEnumerable<PresenceActivity> activities, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(activities);
        var open = _sessions.TryGetValue(userId, out var existing) ? existing : [];
        var current = activities.Distinct().ToList();

        var changes = new List<PresenceChange>();
        foreach (var (activity, startedAt) in open.Where(session => !current.Contains(session.Key)).ToList())
        {
            open.Remove(activity);
            changes.Add(new PresenceSessionClosed(userId, activity, startedAt, at));
        }

        foreach (var activity in current.Where(activity => !open.ContainsKey(activity)))
        {
            open[activity] = at;
            changes.Add(new PresenceSessionOpened(userId, activity, at));
        }

        if (open.Count == 0)
        {
            _sessions.Remove(userId);
        }
        else
        {
            _sessions[userId] = open;
        }

        return changes;
    }

    /// <summary>
    /// Closes every open session of the user and forgets them (tracking stopped).
    /// </summary>
    public IReadOnlyList<PresenceChange> Stop(Guid userId, DateTimeOffset at) =>
        _sessions.Remove(userId, out var open) ? Close(userId, open, at) : [];

    /// <summary>
    /// Closes every session and forgets everyone (shutdown).
    /// </summary>
    public IReadOnlyList<PresenceChange> StopAll(DateTimeOffset at)
    {
        var changes = _sessions.SelectMany(user => Close(user.Key, user.Value, at)).ToList();
        _sessions.Clear();
        return changes;
    }

    /// <summary>
    /// An independent copy, so a caller can apply changes to it and keep the original until they are stored.
    /// </summary>
    public PresenceSessionStateMachine Clone()
    {
        var copy = new PresenceSessionStateMachine();
        foreach (var (userId, open) in _sessions)
        {
            copy._sessions[userId] = new Dictionary<PresenceActivity, DateTimeOffset>(open);
        }

        return copy;
    }

    private static List<PresenceChange> Close(Guid userId, Dictionary<PresenceActivity, DateTimeOffset> open, DateTimeOffset at) =>
        [.. open.Select(session => new PresenceSessionClosed(userId, session.Key, session.Value, at))];
}
