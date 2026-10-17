namespace Apollo.Web.Client.Contracts;

/// <summary>
/// <c>GET /api/admin/data/members</c> (#32), one per platform with a synced guild: current members without bots, split
/// by the tracking gate's rule (scope §5.1.1).
/// </summary>
/// <param name="Platform">The platform name, e.g. <c>Discord</c>.</param>
/// <param name="Tracked">Members the bot records: holders of the tracking role; 0 without one.</param>
/// <param name="Untracked">The other members.</param>
/// <param name="Total">All members, <paramref name="Tracked"/> plus <paramref name="Untracked"/>.</param>
/// <param name="Mode">Whether a tracking role is set, and whether it still exists.</param>
public sealed record MemberTrackingDto(string Platform, int Tracked, int Untracked, int Total, RoleSettingsMode Mode);
