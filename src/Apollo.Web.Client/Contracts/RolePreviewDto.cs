namespace Apollo.Web.Client.Contracts;

/// <summary>Response of <c>GET /api/admin/settings/{platform}/preview?roleIds=…</c> (#33). Bots are not counted.</summary>
/// <param name="Members">Members holding at least one of the roles.</param>
/// <param name="NewlyTracked">If the roles were the tracking role: members not tracked now who would be.</param>
/// <param name="NoLongerTracked">If the roles were the tracking role: members tracked now who would no longer be.</param>
public sealed record RolePreviewDto(int Members, int NewlyTracked, int NoLongerTracked);
