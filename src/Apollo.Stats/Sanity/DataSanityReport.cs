namespace Apollo.Stats.Sanity;

/// <summary>The checks of <see cref="IDataSanityService"/> over one range (#40).</summary>
/// <param name="HeartbeatGaps">Consecutive heartbeats further apart than the threshold (bot restarts, scope §5.1).</param>
/// <param name="LongSessions">Closed voice and presence sessions longer than 24 h that started in the range.</param>
/// <param name="OpenSessions">Voice sessions still open after more than 24 h (presence rows are only written on close).</param>
/// <param name="UntrackedEvents">Rows of a user who had no tracking period at the time (scope §5.1.1): must be 0.</param>
/// <param name="UntrackedReferences">References to a user who had no tracking period at the time: must be 0.</param>
public sealed record DataSanityReport(
    SanityCheck<HeartbeatGap> HeartbeatGaps,
    SanityCheck<SanitySession> LongSessions,
    SanityCheck<SanitySession> OpenSessions,
    SanityCheck<UntrackedRow> UntrackedEvents,
    SanityCheck<UntrackedRow> UntrackedReferences);
