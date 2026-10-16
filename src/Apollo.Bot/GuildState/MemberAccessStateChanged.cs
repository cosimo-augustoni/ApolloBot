namespace Apollo.Bot.GuildState;

/// <summary>
/// A member's access state changed: they joined or left, their roles (<c>member_roles</c>) changed, or their
/// Administrator flag did. Published once per member and applied change, never for a no-op.
/// </summary>
/// <param name="GuildId">The internal guild ID.</param>
/// <param name="UserId">The internal user ID.</param>
public sealed record MemberAccessStateChanged(Guid GuildId, Guid UserId) : GuildStateNotification(GuildId);
