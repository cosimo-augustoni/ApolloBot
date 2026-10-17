namespace Apollo.Web.Client.Contracts;

/// <summary>One entry of <c>GET /api/admin/settings/{platform}/history</c> (#33), newest first.</summary>
/// <param name="ChangedAt">When the change was made (UTC).</param>
/// <param name="ChangedBy">The admin's display name; <see langword="null"/> for changes made outside the admin page.</param>
/// <param name="Setting">Which setting changed.</param>
/// <param name="Action">Whether the role was added or removed.</param>
/// <param name="RoleName">The role's current name.</param>
public sealed record RoleSettingChangeDto(
    DateTimeOffset ChangedAt,
    string? ChangedBy,
    RoleSettingKind Setting,
    RoleSettingChangeAction Action,
    string RoleName);
