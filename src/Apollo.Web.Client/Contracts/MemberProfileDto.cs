namespace Apollo.Web.Client.Contracts;

/// <summary>A member as the head of their stats page shows them (#38).</summary>
/// <param name="Id">The internal user ID; platform IDs are never exposed.</param>
/// <param name="DisplayName">The synced display name; <see langword="null"/> before the guild sync named the user.</param>
/// <param name="AvatarUrl">The synced avatar; <see langword="null"/> without one.</param>
/// <param name="Platform">The platform the member is on, e.g. <c>Discord</c>.</param>
public sealed record MemberProfileDto(Guid Id, string? DisplayName, string? AvatarUrl, string? Platform);
