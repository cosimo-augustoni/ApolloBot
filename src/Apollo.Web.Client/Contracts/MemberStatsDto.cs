namespace Apollo.Web.Client.Contracts;

/// <summary>
/// One member's stats page (#38, <c>GET /api/admin/members/{userId}/stats</c>): who they are, whether they are tracked,
/// and their activity and highlights over a <c>Europe/Zurich</c> date range, bots left out.
/// </summary>
/// <param name="Member">The member's synced name and avatar.</param>
/// <param name="Tracking">Whether and since when they are tracked (scope §5.1.1), whatever the range.</param>
/// <param name="Activity">Their activity (#35) over the range.</param>
/// <param name="Highlights">Their raw-data stats (#36) over the range.</param>
public sealed record MemberStatsDto(MemberProfileDto Member, TrackingStatusDto Tracking, ActivityStatsDto Activity, MemberHighlightsDto Highlights);
