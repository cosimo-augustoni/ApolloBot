using Apollo.Data.Tracking;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Admin;

/// <summary>
/// Whether the bot can give and take the tracking role (scope §5.1.2, #33): it needs Manage Roles (or Administrator),
/// and its highest role must sit above the tracking role, since a platform lets nobody but the owner manage a role at or
/// above their own highest one. Computed from the synced guild state, so it is as current as the role sync.
/// </summary>
public static class BotPermissionCheck
{
    /// <param name="bot">The bot's role facts; <see langword="null"/> when the bot is not a synced member, which fails both checks.</param>
    /// <param name="trackingRole">The configured tracking role; without one, or when it was deleted, the hierarchy is not checked.</param>
    public static BotPermissionCheckDto Evaluate(BotRoleFacts? bot, SettingsRole? trackingRole)
    {
        bool? isAboveTrackingRole = trackingRole is { IsDeleted: false }
            ? bot?.HighestRolePosition > trackingRole.Position
            : null;
        return new BotPermissionCheckDto(bot?.ManagesRoles == true, isAboveTrackingRole);
    }
}
