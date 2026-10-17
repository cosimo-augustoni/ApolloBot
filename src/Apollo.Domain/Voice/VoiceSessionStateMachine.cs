namespace Apollo.Domain.Voice;

/// <summary>
/// Turns voice connection changes into session and segment changes (scope §5.1, #16): join, leave, move and flag
/// changes. It holds the voice connections of tracked users only; the caller drops untracked users before calling it.
/// Not thread-safe: the caller serialises calls.
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
        return Diff(userId, before, Current(userId), at, leftReason: VoiceSessionEndReason.Left, movedReason: VoiceSessionEndReason.Moved);
    }

    /// <summary>
    /// Replaces all connections of the user with the platform's current ones, e.g. when tracking starts or after the bot
    /// reconnected. A session that no longer matches closes with <paramref name="closeReason"/>.
    /// </summary>
    public IReadOnlyList<VoiceChange> Reconcile(
        Guid userId,
        IEnumerable<VoiceConnection> connections,
        DateTimeOffset at,
        VoiceSessionEndReason closeReason)
    {
        ArgumentNullException.ThrowIfNull(connections);
        var before = Current(userId);
        Store(userId, [.. connections.Where(c => c.ChannelId is not null)]);
        return Diff(userId, before, Current(userId), at, leftReason: closeReason, movedReason: closeReason);
    }

    /// <summary>
    /// Closes the user's session with <paramref name="reason"/> and forgets their connections (tracking stopped).
    /// </summary>
    public IReadOnlyList<VoiceChange> Stop(Guid userId, DateTimeOffset at, VoiceSessionEndReason reason) =>
        _connections.Remove(userId) ? [new VoiceSessionClosed(userId, at, reason)] : [];

    /// <summary>
    /// Closes every session with <paramref name="reason"/> and forgets everyone (shutdown).
    /// </summary>
    public IReadOnlyList<VoiceChange> StopAll(DateTimeOffset at, VoiceSessionEndReason reason)
    {
        var changes = _connections.Keys.Select(userId => (VoiceChange)new VoiceSessionClosed(userId, at, reason)).ToList();
        _connections.Clear();
        return changes;
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

    private static List<VoiceChange> Diff(
        Guid userId,
        VoiceConnection? before,
        VoiceConnection? after,
        DateTimeOffset at,
        VoiceSessionEndReason leftReason,
        VoiceSessionEndReason movedReason)
    {
        switch (before, after)
        {
            case (null, null):
                return [];
            case (null, { ChannelId: { } channelId }):
                return [new VoiceSessionOpened(userId, at, channelId, after.Flags)];
            case (not null, null):
                return [new VoiceSessionClosed(userId, at, leftReason)];
            case (not null, { ChannelId: { } channelId }) when before.ChannelId != channelId:
                return [new VoiceSessionClosed(userId, at, movedReason), new VoiceSessionOpened(userId, at, channelId, after.Flags)];
            case (not null, not null) when before.Flags != after.Flags:
                return [new VoiceSegmentChanged(userId, at, after.Flags)];
            default:
                return [];
        }
    }
}
