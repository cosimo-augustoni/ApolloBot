namespace Apollo.Domain.Voice;

/// <summary>
/// Turns voice connection changes into session and segment changes (scope §5.1, #16): join, leave, move and flag
/// changes, and the reconciliation rule for what the bot could not see or store (#98). It holds the voice connections of
/// tracked users only; the caller drops untracked users before calling it, and reconciles the sessions taken over on
/// startup (<see cref="Restore"/>) with <see cref="ReconcileUntracked"/> when their member is no longer tracked. Not
/// thread-safe: the caller serialises calls.
/// </summary>
/// <remarks>
/// A user is in voice while any of their connections is in a channel (Fluxer may report several). The session follows
/// the user's oldest connection that is in a channel: its channel and flags count, the others are ignored until it
/// leaves. A session is one continuous stay in one channel; every change of mute, deaf, stream or video starts a new
/// segment.
/// </remarks>
public sealed class VoiceSessionStateMachine
{
    // Per user, the connections that are in a channel, oldest first.
    private readonly Dictionary<Guid, List<VoiceConnection>> _connections = [];

    // Per user in voice, when their current segment started: a reconciliation never closes it earlier.
    private readonly Dictionary<Guid, DateTimeOffset> _segmentStarts = [];

    public int OpenSessionCount => _connections.Count;

    public IReadOnlyCollection<Guid> UsersInVoice => [.. _connections.Keys];

    /// <summary>
    /// One connection of the user changed (a voice state update): leaving closes the session as
    /// <see cref="VoiceSessionEndReason.Left"/>, a channel change closes it as <see cref="VoiceSessionEndReason.Moved"/>.
    /// </summary>
    public IReadOnlyList<VoiceChange> Update(Guid userId, VoiceConnection connection, DateTimeOffset at)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var before = Current(userId);
        var connections = _connections.TryGetValue(userId, out var existing) ? existing : [];
        var index = connections.FindIndex(c => c.ConnectionId == connection.ConnectionId);
        if (connection.ChannelId is null)
        {
            if (index >= 0)
            {
                connections.RemoveAt(index);
            }
        }
        else if (index >= 0)
        {
            connections[index] = connection;
        }
        else
        {
            connections.Add(connection);
        }

        Store(userId, connections);
        var changes = Diff(userId, before, Current(userId), at);
        Track(changes);
        return changes;
    }

    /// <summary>
    /// The reconciliation rule (#98): compares the session the machine holds with the platform's current connections
    /// after a time the bot could not see or store changes, and replaces the user's connections with them.
    /// </summary>
    /// <remarks>
    /// A member still in the same channel keeps their session if <see cref="VoiceBlindSpot.Continues"/>; flags that
    /// differ start a new segment at <see cref="VoiceBlindSpot.Now"/>. Anything else (left, another channel, a blind spot
    /// too long) closes the session as <see cref="VoiceSessionEndReason.Unobserved"/> at T, the later of
    /// <see cref="VoiceBlindSpot.LastKnownAt"/> and the start of its current segment, and a member in voice gets a new
    /// session from <see cref="VoiceBlindSpot.Now"/>.
    /// </remarks>
    public IReadOnlyList<VoiceChange> Reconcile(Guid userId, IEnumerable<VoiceConnection> connections, VoiceBlindSpot blindSpot)
    {
        ArgumentNullException.ThrowIfNull(connections);
        ArgumentNullException.ThrowIfNull(blindSpot);
        var before = Current(userId);
        var closeAt = CloseAt(userId, blindSpot);
        var continues = blindSpot.Continues(_segmentStarts.GetValueOrDefault(userId, DateTimeOffset.MinValue));
        Store(userId, [.. connections.Where(c => c.ChannelId is not null)]);
        var after = Current(userId);
        var now = blindSpot.Now;

        List<VoiceChange> changes = (before, after) switch
        {
            (null, null) => [],
            (null, { ChannelId: { } channelId }) => [new VoiceSessionOpened(userId, now, channelId, after.Flags)],
            (not null, null) => [new VoiceSessionClosed(userId, closeAt, VoiceSessionEndReason.Unobserved)],
            (not null, not null) when before.ChannelId == after.ChannelId && continues =>
                before.Flags == after.Flags ? [] : [new VoiceSegmentChanged(userId, now, after.Flags)],
            (not null, { ChannelId: { } channelId }) =>
            [
                new VoiceSessionClosed(userId, closeAt, VoiceSessionEndReason.Unobserved),
                new VoiceSessionOpened(userId, now, channelId, after.Flags),
            ],
            _ => [],
        };
        Track(changes);
        return changes;
    }

    /// <summary>
    /// The rule for a member who is no longer tracked: their session closes as
    /// <see cref="VoiceSessionEndReason.TrackingStopped"/> at T (see <see cref="Reconcile"/>), since nothing after the
    /// last moment the bot knew they were tracked may be recorded, and the machine forgets them.
    /// </summary>
    public IReadOnlyList<VoiceChange> ReconcileUntracked(Guid userId, VoiceBlindSpot blindSpot)
    {
        ArgumentNullException.ThrowIfNull(blindSpot);
        return Stop(userId, CloseAt(userId, blindSpot), VoiceSessionEndReason.TrackingStopped);
    }

    /// <summary>
    /// Holds a session taken over from the database on startup (#98): one connection in <paramref name="channelId"/>
    /// whose current segment started at <paramref name="segmentStartedAt"/>. The first reconciliation then decides
    /// whether it continues.
    /// </summary>
    public void Restore(Guid userId, Guid channelId, VoiceFlags flags, DateTimeOffset segmentStartedAt)
    {
        _connections[userId] = [new VoiceConnection(ConnectionId: null, channelId, flags)];
        _segmentStarts[userId] = segmentStartedAt;
    }

    /// <summary>
    /// Closes the user's session with <paramref name="reason"/> and forgets their connections (tracking stopped).
    /// </summary>
    public IReadOnlyList<VoiceChange> Stop(Guid userId, DateTimeOffset at, VoiceSessionEndReason reason)
    {
        _segmentStarts.Remove(userId);
        return _connections.Remove(userId) ? [new VoiceSessionClosed(userId, at, reason)] : [];
    }

    /// <summary>
    /// An independent copy, so a caller can apply changes to it and keep the original until they are stored.
    /// </summary>
    public VoiceSessionStateMachine Clone()
    {
        var copy = new VoiceSessionStateMachine();
        foreach (var (userId, connections) in _connections)
        {
            copy._connections[userId] = [.. connections];
        }

        foreach (var (userId, startedAt) in _segmentStarts)
        {
            copy._segmentStarts[userId] = startedAt;
        }

        return copy;
    }

    private VoiceConnection? Current(Guid userId) =>
        _connections.TryGetValue(userId, out var connections) ? connections[0] : null;

    private void Store(Guid userId, List<VoiceConnection> connections)
    {
        if (connections.Count == 0)
        {
            _connections.Remove(userId);
        }
        else
        {
            _connections[userId] = connections;
        }
    }

    private DateTimeOffset CloseAt(Guid userId, VoiceBlindSpot blindSpot) =>
        _segmentStarts.TryGetValue(userId, out var segmentStartedAt) && segmentStartedAt > blindSpot.LastKnownAt
            ? segmentStartedAt
            : blindSpot.LastKnownAt;

    /// <summary>
    /// Keeps <c>_segmentStarts</c> in step with <paramref name="changes"/>.
    /// </summary>
    private void Track(IEnumerable<VoiceChange> changes)
    {
        foreach (var change in changes)
        {
            switch (change)
            {
                case VoiceSessionOpened or VoiceSegmentChanged:
                    _segmentStarts[change.UserId] = change.At;
                    break;
                case VoiceSessionClosed when !_connections.ContainsKey(change.UserId):
                    _segmentStarts.Remove(change.UserId);
                    break;
            }
        }
    }

    private static List<VoiceChange> Diff(Guid userId, VoiceConnection? before, VoiceConnection? after, DateTimeOffset at)
    {
        switch (before, after)
        {
            case (null, null):
                return [];
            case (null, { ChannelId: { } channelId }):
                return [new VoiceSessionOpened(userId, at, channelId, after.Flags)];
            case (not null, null):
                return [new VoiceSessionClosed(userId, at, VoiceSessionEndReason.Left)];
            case (not null, { ChannelId: { } channelId }) when before.ChannelId != channelId:
                return [new VoiceSessionClosed(userId, at, VoiceSessionEndReason.Moved), new VoiceSessionOpened(userId, at, channelId, after.Flags)];
            case (not null, not null) when before.Flags != after.Flags:
                return [new VoiceSegmentChanged(userId, at, after.Flags)];
            default:
                return [];
        }
    }
}
