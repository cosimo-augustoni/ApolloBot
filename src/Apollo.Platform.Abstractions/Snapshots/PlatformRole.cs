namespace Apollo.Platform.Abstractions.Snapshots;

/// <summary>
/// A guild role.
/// </summary>
/// <param name="Role">The role.</param>
/// <param name="Name">The role name.</param>
/// <param name="Position">The sort position; higher is listed first.</param>
/// <param name="IsAdministrator">Whether the role grants the Administrator permission.</param>
/// <param name="ManagesRoles">
/// Whether the role grants the Manage Roles permission, which the bot needs for the tracking role (#21, #33).
/// </param>
/// <param name="IsManaged">Whether the role belongs to an integration or bot and cannot be assigned by hand.</param>
/// <param name="Color">The role colour as 0xRRGGBB; <see langword="null"/> when the role has none.</param>
public sealed record PlatformRole(PlatformRef Role, string Name, int Position, bool IsAdministrator, bool ManagesRoles, bool IsManaged, int? Color);
