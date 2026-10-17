namespace Apollo.Web.Client.Contracts;

/// <summary>A synced role on the admin settings page (#33).</summary>
/// <param name="Id">The internal role ID; platform IDs are never exposed.</param>
/// <param name="Name">The role name.</param>
/// <param name="Color">The role colour as 0xRRGGBB; <see langword="null"/> when the role has none.</param>
/// <param name="Position">The sort position; higher is listed first.</param>
/// <param name="IsDeleted">Whether the role was deleted on the platform; it can no longer be chosen.</param>
/// <param name="MemberCount">The members holding the role, bots excluded.</param>
public sealed record SettingsRoleDto(Guid Id, string Name, int? Color, int Position, bool IsDeleted, int MemberCount);
