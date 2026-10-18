namespace Apollo.Data.Activity;

/// <summary>
/// The refresh policy of the activity continuous aggregates (#34). A policy run re-materializes the invalidated
/// buckets between <see cref="RefreshStartOffset"/> and <see cref="RefreshEndOffset"/> ago; anything newer than the
/// materialized data is read from the raw tables at query time (real-time aggregation), so today is always current.
/// </summary>
/// <remarks>
/// Only <c>hourly_activity_heatmap</c> (configured in <c>ActivityModel</c>) reads these constants. The four hand-written
/// Zurich-day aggregates get their policies in the <c>AddActivityAggregates</c> migration as literal values, which must
/// stay the same as these (a merged migration cannot reference code that may change);
/// <c>ContinuousAggregates_WhenMigrated_ShouldEachHaveARefreshPolicy</c> checks that all five agree.
/// </remarks>
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
