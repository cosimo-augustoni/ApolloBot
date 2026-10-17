using System.Text.Json.Serialization;

namespace Apollo.Web.Client.Contracts;

/// <summary>
/// Whether the bot can give and take the tracking role (scope §5.1.2), from the synced guild state (#33).
/// </summary>
/// <param name="CanManageRoles">The bot has Manage Roles (or Administrator).</param>
/// <param name="IsAboveTrackingRole">
/// The bot's highest role is above the tracking role; <see langword="null"/> when no (existing) tracking role is set.
/// </param>
public sealed record BotPermissionCheckDto(bool CanManageRoles, bool? IsAboveTrackingRole)
{
    /// <summary>Whether the page shows no permission or hierarchy warning.</summary>
    [JsonIgnore]
    public bool IsOk => CanManageRoles && IsAboveTrackingRole != false;
}
