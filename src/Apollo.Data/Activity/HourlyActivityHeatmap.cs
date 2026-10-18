namespace Apollo.Data.Activity;

/// <summary>
/// One row of the <c>hourly_activity_heatmap</c> continuous aggregate (scope §5.3, #34): messages one user sent in one
/// hour. The stats layer (#35) folds the rows into Zurich weekday × hour.
/// </summary>
public sealed class HourlyActivityHeatmap
{
    /// <summary>
    /// The UTC start of the hour. Zurich is always a whole number of hours off UTC, so it is also the start of a Zurich
    /// hour; convert it with <see cref="TimeZoneInfo"/> for the weekday and hour (on the fall-back day two buckets map to
    /// Zurich 02:00, on the spring-forward day none does).
    /// </summary>
    public DateTimeOffset Hour { get; init; }

    public Guid GuildId { get; init; }

    public Guid UserId { get; init; }

    /// <summary>
    /// The message rows' <c>is_bot</c>. Stats leave bots out by default (scope §5.1).
    /// </summary>
    public bool IsBot { get; init; }

    /// <inheritdoc cref="DailyUserChannelActivity.MessageCount"/>
    public long MessageCount { get; init; }
}
