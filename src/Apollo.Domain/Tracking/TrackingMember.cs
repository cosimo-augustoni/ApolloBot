namespace Apollo.Domain.Tracking;

/// <summary>
/// The facts about a member that <see cref="TrackingRule"/> decides on.
/// </summary>
/// <param name="IsCurrentMember">Whether the user is in the guild (<c>guild_members.left_at IS NULL</c>).</param>
/// <param name="RoleIds">The internal IDs of the roles the member holds.</param>
public sealed record TrackingMember(bool IsCurrentMember, IReadOnlySet<Guid> RoleIds);
