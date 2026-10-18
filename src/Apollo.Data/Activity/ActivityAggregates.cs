namespace Apollo.Data.Activity;

/// <summary>
/// The refresh policy every activity continuous aggregate shares (#34). A policy run re-materializes the invalidated
/// buckets between <see cref="RefreshStartOffset"/> and <see cref="RefreshEndOffset"/> ago; anything newer than the
/// materialized data is read from the raw tables at query time (real-time aggregation), so today is always current.
/// </summary>
public static class ActivityAggregates
{
    /// <summary>
    /// How far back a run looks: a week covers whole Zurich days, late reactions to older messages and voice sessions
    /// that close up to a week after they started.
    /// </summary>
    public const string RefreshStartOffset = "7 days";

    public const string RefreshEndOffset = "1 hour";

    public const string RefreshScheduleInterval = "30 minutes";
}
