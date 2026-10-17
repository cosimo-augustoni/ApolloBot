using Apollo.Domain.Tracking;

namespace Apollo.Data.Tracking;

/// <summary>The role settings of one <c>(guild, platform)</c> with everything the admin settings page shows (#33).</summary>
/// <param name="Roles">The platform's synced roles of the guild, deleted ones included, highest position first.</param>
/// <param name="TrackingRoleId">The configured tracking role, deleted or not; <see langword="null"/> when none is set.</param>
/// <param name="MemberAccessRoleIds">The configured member access roles.</param>
/// <param name="Mode">Who is tracked, as the bot's gate decides it (<see cref="TrackingRule.ModeOf"/>).</param>
/// <param name="Bot">The bot's own role facts; <see langword="null"/> when the bot is not a synced member yet.</param>
public sealed record RoleSettingsOverview(
    IReadOnlyList<SettingsRole> Roles,
    Guid? TrackingRoleId,
    IReadOnlyList<Guid> MemberAccessRoleIds,
    TrackingMode Mode,
    BotRoleFacts? Bot);
