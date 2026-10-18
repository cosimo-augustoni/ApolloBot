namespace Apollo.Web.Client.Contracts;

/// <summary>A user as the admin pages show them: the data page (#32) and the member page (#38).</summary>
/// <param name="Id">The internal user ID; platform IDs are never exposed.</param>
/// <param name="DisplayName">The synced display name; <see langword="null"/> before the guild sync named the user.</param>
public sealed record EventUserDto(Guid Id, string? DisplayName);
