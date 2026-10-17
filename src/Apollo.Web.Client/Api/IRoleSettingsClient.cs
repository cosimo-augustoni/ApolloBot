using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Client.Api;

/// <summary>
/// The role settings of the admin page (#33, <c>/api/admin/settings</c>): per platform the tracking role and the member
/// access roles, with preview and change history. <c>platform</c> is a name from <see cref="GetPlatformsAsync"/>.
/// </summary>
public interface IRoleSettingsClient
{
    /// <summary>The platforms whose guild the bot has synced, e.g. <c>Discord</c>.</summary>
    Task<IReadOnlyList<string>> GetPlatformsAsync(CancellationToken cancellationToken);

    Task<RoleSettingsDto> GetSettingsAsync(string platform, CancellationToken cancellationToken);

    Task<RolePreviewDto> GetPreviewAsync(string platform, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);

    /// <summary>The <paramref name="take"/> newest changes, newest first.</summary>
    Task<IReadOnlyList<RoleSettingChangeDto>> GetHistoryAsync(string platform, int take, CancellationToken cancellationToken);

    /// <summary>Sets the tracking role; <see langword="null"/> clears it.</summary>
    /// <exception cref="RoleSettingsRejectedException">The host rejected the role (deleted, or not of this guild and platform).</exception>
    /// <exception cref="RoleSettingsConflictException">Another admin saved the same settings at the same time; nothing was kept.</exception>
    Task SetTrackingRoleAsync(string platform, Guid? roleId, CancellationToken cancellationToken);

    /// <summary>Replaces the member access roles with <paramref name="roleIds"/>.</summary>
    /// <exception cref="RoleSettingsRejectedException">The host rejected a role (deleted, or not of this guild and platform).</exception>
    /// <exception cref="RoleSettingsConflictException">Another admin saved the same settings at the same time; nothing was kept.</exception>
    Task SetMemberAccessRolesAsync(string platform, IReadOnlyCollection<Guid> roleIds, CancellationToken cancellationToken);
}
