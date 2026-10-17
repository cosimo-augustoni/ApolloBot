namespace Apollo.Web.Client.Contracts;

/// <summary>Response of <c>GET /api/admin/settings/{platform}</c> (#33).</summary>
/// <param name="Platform">The platform, as in the route (e.g. <c>Discord</c>).</param>
/// <param name="Roles">The synced roles, deleted ones included, highest position first.</param>
/// <param name="TrackingRoleId">The tracking role (possibly deleted); <see langword="null"/> when none is set.</param>
/// <param name="MemberAccessRoleIds">The member access roles.</param>
/// <param name="Mode">Who is tracked now.</param>
/// <param name="BotPermissions">Whether the bot can manage the tracking role.</param>
public sealed record RoleSettingsDto(
    string Platform,
    IReadOnlyList<SettingsRoleDto> Roles,
    Guid? TrackingRoleId,
    IReadOnlyList<Guid> MemberAccessRoleIds,
    RoleSettingsMode Mode,
    BotPermissionCheckDto BotPermissions);
