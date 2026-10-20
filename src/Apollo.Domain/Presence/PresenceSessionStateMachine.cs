using System.Collections.Immutable;

namespace Apollo.Domain.Presence;

/// <summary>
/// Turns presence updates into activity sessions (scope §5.1, #19): the diff of a user's old and new activity sets. An
/// activity that appears opens a session, one that disappears closes it, so a switch from one game to another closes
/// and opens at the same instant, and simultaneous activities are independent sessions. A session is identified by
/// user, activity type and name. It holds tracked users only; the caller drops untracked users and custom statuses
/// before calling it, and passes no activities for a user who went offline. Not thread-safe: the caller serialises calls.
/// </summary>
/// <remarks>
/// Every session is split into <see cref="PresenceSegment"/>s of unchanged rich presence (#115): one segment is open per
/// open session, and a change of a field that <see cref="PresenceActivityDetails.IsSameSegmentAs"/> compares ends it and
/// starts the next at the same instant, so a new song or match never splits the session itself. A segment keeps the
/// details it started with. A closed session carries all of its segments, covering it from start to end without gaps.
/// </remarks>
public sealed class PresenceSessionStateMachine
{
    /// <summary>
    /// The shortest session that is stored (scope §5.1 notes, D1).
    /// </summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(1);

    // Per user, every open session by its activity.
    private readonly Dictionary<Guid, Dictionary<PresenceActivity, OpenSession>> _sessions = [];

    public int OpenSessionCount => _sessions.Values.Sum(sessions => sessions.Count);

    public IReadOnlyCollection<Guid> Users => [.. _sessions.Keys];

    /// <summary>
    /// The user's activities are now <paramref name="observed"/> (a presence update, or the current presence when
    /// tracking starts or after a reconnect). Of an activity reported twice, the first counts.
    /// </summary>
    public IReadOnlyList<PresenceChange> Update(Guid userId, IEnumerable<ObservedActivity> observed, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(observed);
        var open = _sessions.TryGetValue(userId, out var existing) ? existing : [];
        var current = observed.DistinctBy(o => o.Activity).ToDictionary(o => o.Activity, o => o.Details);

        var changes = new List<PresenceChange>();
        foreach (var (activity, session) in open.Where(session => !current.ContainsKey(session.Key)).ToList())
        {
            open.Remove(activity);
            changes.Add(session.Close(userId, activity, at));
        }

        foreach (var (activity, details) in current)
        {
            if (open.TryGetValue(activity, out var session))
            {
                open[activity] = session.Observe(details, at);
            }
            else
            {
                open[activity] = OpenSession.Start(details, at);
                changes.Add(new PresenceSessionOpened(userId, activity, at));
            }
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
    /// The periodic reconciliation (#98): the user's current activities are <paramref name="observed"/>, read from the
    /// platform while the gateway stayed connected, so only a failed save can have been missed. A session whose
    /// activity is gone closes at <paramref name="lastKnownAt"/> (the previous run), never before its own start; an
    /// activity without a session opens one at <paramref name="now"/>; a session whose rich presence differs starts a
    /// new segment at <paramref name="now"/>; matching sessions stay as they are.
    /// </summary>
    public IReadOnlyList<PresenceChange> Reconcile(
        Guid userId,
        IEnumerable<ObservedActivity> observed,
        DateTimeOffset lastKnownAt,
        DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(observed);
        var current = observed.ToList();
        var open = _sessions.TryGetValue(userId, out var existing) ? existing : [];
        var changes = new List<PresenceChange>();
        foreach (var (activity, session) in open.Where(session => current.All(o => o.Activity != session.Key)).ToList())
        {
            open.Remove(activity);
            changes.Add(session.Close(userId, activity, lastKnownAt > session.StartedAt ? lastKnownAt : session.StartedAt));
        }

        changes.AddRange(Update(userId, current, now));
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
            copy._sessions[userId] = new Dictionary<PresenceActivity, OpenSession>(open);
        }

        return copy;
    }

    private static List<PresenceChange> Close(Guid userId, Dictionary<PresenceActivity, OpenSession> open, DateTimeOffset at) =>
        [.. open.Select(session => session.Value.Close(userId, session.Key, at))];

    /// <summary>
    /// An open session: its start, its finished segments and its open one. Immutable, so <see cref="Clone"/> can share it.
    /// </summary>
    private sealed record OpenSession(
        DateTimeOffset StartedAt,
        ImmutableList<PresenceSegment> Finished,
        DateTimeOffset SegmentStartedAt,
        PresenceActivityDetails Details)
    {
        public static OpenSession Start(PresenceActivityDetails details, DateTimeOffset at) => new(at, [], at, details);

        public OpenSession Observe(PresenceActivityDetails details, DateTimeOffset at) =>
            Details.IsSameSegmentAs(details)
                ? this
                : new(StartedAt, Finished.Add(new PresenceSegment(SegmentStartedAt, at, Details)), at, details);

        /// <summary>
        /// The closed session with every segment that started before <paramref name="at"/> (a reconciliation can close a
        /// session before a later change), the last one ending at <paramref name="at"/>. The first segment always stays.
        /// </summary>
        public PresenceSessionClosed Close(Guid userId, PresenceActivity activity, DateTimeOffset at)
        {
            var segments = Finished
                .Add(new PresenceSegment(SegmentStartedAt, at, Details))
                .Where((segment, index) => index == 0 || segment.From < at)
                .ToList();
            segments[^1] = segments[^1] with { To = at };
            return new PresenceSessionClosed(userId, activity, StartedAt, at, segments);
        }
    }
}
