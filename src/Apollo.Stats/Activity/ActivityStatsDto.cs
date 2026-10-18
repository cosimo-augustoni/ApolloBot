namespace Apollo.Stats.Activity;

/// <summary>
/// The activity of a guild or a user over a Zurich date range (scope §5.3), as <see cref="IActivityStatsService"/>
/// returns it. Plain records and lists only, so it round-trips through JSON into recap snapshots (#46).
/// </summary>
public sealed record ActivityStatsDto
{
    public required DateOnly From { get; init; }

    public required DateOnly To { get; init; }

    public required ActivityCounts Totals { get; init; }

    /// <summary>
    /// Per day: for the guild over every day of the range, for a user over <see cref="TrackedDays"/>.
    /// </summary>
    public required ActivityAverages Averages { get; init; }

    /// <summary>
    /// Days on which a message was sent, a reaction added or a voice session started.
    /// </summary>
    public required int ActiveDays { get; init; }

    /// <summary>
    /// For the guild: distinct users with activity in the range (as for <see cref="ActiveDays"/>); <see langword="null"/>
    /// for a user.
    /// </summary>
    public required int? ActiveUsers { get; init; }

    /// <summary>
    /// For a user: the Zurich days in the range that overlap one of their tracking periods (scope §5.1.1);
    /// <see langword="null"/> for the guild.
    /// </summary>
    public required int? TrackedDays { get; init; }

    /// <summary>
    /// For a user: the Zurich day their first tracking period started (whatever the range); <see langword="null"/> for
    /// the guild and for a user who was never tracked.
    /// </summary>
    public required DateOnly? TrackedSince { get; init; }

    /// <summary>
    /// One entry per day of the range, oldest first, zero-filled.
    /// </summary>
    public required IReadOnlyList<DailyActivity> Daily { get; init; }

    /// <summary>
    /// Channels with messages or voice time, most messages first, then most voice time.
    /// </summary>
    public required IReadOnlyList<ChannelActivity> Channels { get; init; }

    /// <summary>
    /// Messages per Zurich weekday and hour: 7 rows (Monday first) of 24 hours (0 to 23).
    /// </summary>
    public required IReadOnlyList<IReadOnlyList<long>> Heatmap { get; init; }
}
