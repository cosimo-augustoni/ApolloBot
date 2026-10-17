namespace Apollo.Data.Tracking;

/// <summary>A synced role as the admin settings page offers it (#33).</summary>
/// <param name="Id">The internal role ID; external IDs are never exposed (scope §5.1.2).</param>
/// <param name="Name">The role name; <see langword="null"/> before the role sync named it.</param>
/// <param name="Color">The role colour as 0xRRGGBB; <see langword="null"/> when the role has none.</param>
/// <param name="Position">The sort position; higher is listed first.</param>
/// <param name="IsDeleted">Whether the role was deleted on the platform.</param>
/// <param name="MemberCount">The current members holding the role, bots excluded.</param>
public sealed record SettingsRole(Guid Id, string? Name, int? Color, int Position, bool IsDeleted, int MemberCount);
