using Apollo.Domain.Tracking;

namespace Apollo.Data.Tracking;

/// <summary>One entry of the role settings change history (#33), read from <c>role_setting_changes</c>.</summary>
/// <param name="ChangedAt">When the change was made.</param>
/// <param name="ChangedBy">The author's display name; <see langword="null"/> for changes made outside the admin page.</param>
/// <param name="Purpose">Which setting changed.</param>
/// <param name="Action">Whether the role was added or removed.</param>
/// <param name="RoleId">The internal role ID.</param>
/// <param name="RoleName">The role's current name.</param>
public sealed record RoleSettingHistoryEntry(
    DateTimeOffset ChangedAt,
    string? ChangedBy,
    RoleSettingPurpose Purpose,
    RoleSettingAction Action,
    Guid RoleId,
    string? RoleName);
