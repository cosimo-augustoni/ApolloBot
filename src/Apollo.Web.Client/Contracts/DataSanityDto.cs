namespace Apollo.Web.Client.Contracts;

/// <summary>
/// The data sanity report of the admin area (#40, <c>GET /api/admin/sanity</c>) over a range of <c>Europe/Zurich</c> days:
/// collection bugs show up here. Each check has a count and up to 20 example rows, newest first.
/// </summary>
/// <param name="From">The first Zurich day of the range.</param>
/// <param name="To">The last Zurich day of the range.</param>
/// <param name="HeartbeatGapThresholdSeconds">How far apart two heartbeats may be before they count as a gap (the stale threshold of the bot status page).</param>
/// <param name="HeartbeatGaps">Consecutive heartbeats further apart than the threshold.</param>
/// <param name="LongSessions">Closed voice and presence sessions longer than 24 h that started in the range.</param>
/// <param name="OpenSessions">Voice sessions open for more than 24 h.</param>
/// <param name="UntrackedEvents">Rows of users without a tracking period at the time: must be 0.</param>
/// <param name="UntrackedReferences">References to users without a tracking period at the time: must be 0.</param>
public sealed record DataSanityDto(
    DateOnly From,
    DateOnly To,
    int HeartbeatGapThresholdSeconds,
    SanityCheckDto<HeartbeatGapDto> HeartbeatGaps,
    SanityCheckDto<SanitySessionDto> LongSessions,
    SanityCheckDto<SanitySessionDto> OpenSessions,
    SanityCheckDto<UntrackedRowDto> UntrackedEvents,
    SanityCheckDto<UntrackedRowDto> UntrackedReferences);
