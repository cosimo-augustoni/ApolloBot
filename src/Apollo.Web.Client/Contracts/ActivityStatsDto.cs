namespace Apollo.Web.Client.Contracts;

/// <summary>
/// The activity of the server, or of one member, over a <c>Europe/Zurich</c> date range: the stats layer's
/// <c>ActivityStatsDto</c> (#35, scope §5.3) as the API sends it. <c>GET /api/admin/stats/server</c> (#37) returns it for
/// the configured guild, bots left out.
/// </summary>
/// <param name="From">The first Zurich day of the range.</param>
/// <param name="To">The last Zurich day of the range, included.</param>
/// <param name="Totals">The summed activity of the range.</param>
/// <param name="Averages">Per day: for the server over every day of the range, for a member over their tracked days.</param>
/// <param name="ActiveDays">Days on which a message was sent, a reaction added or a voice session started.</param>
/// <param name="ActiveUsers">For the server: distinct members with activity in the range; <see langword="null"/> for a member.</param>
/// <param name="TrackedDays">For a member: the days of the range they were tracked on; <see langword="null"/> for the server.</param>
/// <param name="TrackedSince">For a member: the day their current tracking started; <see langword="null"/> otherwise.</param>
/// <param name="Daily">One entry per day of the range, oldest first, zero-filled.</param>
/// <param name="Channels">Channels with messages or voice time, threads rolled up, most messages first.</param>
/// <param name="Heatmap">Messages per Zurich weekday and hour: 7 rows (Monday first) of 24 hours (0 to 23).</param>
public sealed record ActivityStatsDto(
    DateOnly From,
    DateOnly To,
    ActivityCountsDto Totals,
    ActivityAveragesDto Averages,
    int ActiveDays,
    int? ActiveUsers,
    int? TrackedDays,
    DateOnly? TrackedSince,
    IReadOnlyList<DailyActivityDto> Daily,
    IReadOnlyList<ChannelActivityDto> Channels,
    IReadOnlyList<IReadOnlyList<long>> Heatmap);
