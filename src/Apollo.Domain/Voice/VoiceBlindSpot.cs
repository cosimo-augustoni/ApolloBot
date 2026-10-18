namespace Apollo.Domain.Voice;

/// <summary>
/// The time the bot could not see or store voice changes, as the reconciliation rule
/// (<see cref="VoiceSessionStateMachine.Reconcile"/>, #98) needs it: since when (<see cref="LastKnownAt"/>, T, the last
/// moment the bot knew the voice states) and whether a session whose member is still in the same channel continues.
/// </summary>
public sealed record VoiceBlindSpot
{
    // null: the gateway stayed connected, so the blind spot has no limit.
    private readonly TimeSpan? _maxGap;

    // The bot does not know when it last saw the voice states.
    private readonly bool _unknown;

    private VoiceBlindSpot(DateTimeOffset lastKnownAt, DateTimeOffset now, TimeSpan? maxGap, bool unknown)
    {
        LastKnownAt = lastKnownAt < now ? lastKnownAt : now;
        Now = now;
        _maxGap = maxGap;
        _unknown = unknown;
    }

    /// <summary>
    /// T: a session that no longer matches ends here (or at its current segment's start, if that is later).
    /// </summary>
    public DateTimeOffset LastKnownAt { get; }

    /// <summary>
    /// When the reconciliation runs: new sessions and segments start here, since the real time of the change is unknown.
    /// </summary>
    public DateTimeOffset Now { get; }

    /// <summary>
    /// The gateway stayed connected since <paramref name="lastKnownAt"/> (the previous run), so only a failed save can
    /// have been missed: a member still in the same channel was there all along.
    /// </summary>
    public static VoiceBlindSpot WhileConnected(DateTimeOffset lastKnownAt, DateTimeOffset now) => new(lastKnownAt, now, maxGap: null, unknown: false);

    /// <summary>
    /// The bot saw nothing since <paramref name="lastKnownAt"/> (a restart or a disconnect): a member still in the same
    /// channel is taken to have stayed only if the blind spot is at most <paramref name="maxGap"/> long.
    /// </summary>
    public static VoiceBlindSpot Outage(DateTimeOffset lastKnownAt, DateTimeOffset now, TimeSpan maxGap) => new(lastKnownAt, now, maxGap, unknown: false);

    /// <summary>
    /// The bot does not know when it last saw the voice states (no heartbeat before the start): every session it did not
    /// see again since <paramref name="closeAt"/> ends there.
    /// </summary>
    public static VoiceBlindSpot Unknown(DateTimeOffset closeAt, DateTimeOffset now) => new(closeAt, now, maxGap: null, unknown: true);

    /// <summary>
    /// Whether a session whose member is still in the same channel continues, given that the bot last saw its state at
    /// <paramref name="knownAt"/> (the start of its current segment). A session the bot opened or changed after T was
    /// seen since, so its own blind spot is only from then on; that keeps a second run over the same blind spot from
    /// splitting what the first one decided.
    /// </summary>
    public bool Continues(DateTimeOffset knownAt)
    {
        if (_unknown)
        {
            return knownAt >= LastKnownAt;
        }

        return _maxGap is not { } maxGap || Now - (knownAt > LastKnownAt ? knownAt : LastKnownAt) <= maxGap;
    }
}
