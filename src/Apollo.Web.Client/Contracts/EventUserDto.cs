namespace Apollo.Web.Client.Contracts;

/// <summary>A user as the admin data page shows it (#32).</summary>
/// <param name="Id">The internal user ID; platform IDs are never exposed.</param>
/// <param name="DisplayName">The synced display name; <see langword="null"/> before the guild sync named the user.</param>
public sealed record EventUserDto(Guid Id, string? DisplayName);
