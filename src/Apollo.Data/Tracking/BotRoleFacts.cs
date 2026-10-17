namespace Apollo.Data.Tracking;

/// <summary>
/// What the synced guild state says about the bot's own member, for the permission check of the admin page (#33).
/// </summary>
/// <param name="ManagesRoles">
/// Whether the bot may give and take roles: one of its roles grants Manage Roles, or it has the Administrator permission.
/// </param>
/// <param name="HighestRolePosition">The position of the bot's highest role; <see langword="null"/> when it holds none.</param>
public sealed record BotRoleFacts(bool ManagesRoles, int? HighestRolePosition);
