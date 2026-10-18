namespace Apollo.Stats.Activity;

/// <summary>
/// One Zurich day of the daily series.
/// </summary>
public sealed record DailyActivity(DateOnly Date, ActivityCounts Counts);
