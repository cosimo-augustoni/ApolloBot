using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;

namespace Apollo.Data.Tracking;

/// <summary>
/// A role setting of one <c>(guild, platform)</c> (scope §5.1.2): the tracking role (at most one) or a member access
/// role. A settings table, not an event table. Every insert, update and delete is written to
/// <see cref="RoleSettingChange"/> by a database trigger, in the same transaction.
/// </summary>
public sealed class RoleSetting
{
    public Guid GuildId { get; init; }

    public PlatformKind Platform { get; init; }

    /// <summary>
    /// The internal role ID; settings never reference platform IDs.
    /// </summary>
    public Guid RoleId { get; init; }

    public RoleSettingPurpose Purpose { get; init; }

    /// <summary>
    /// Who set it; <see langword="null"/> for rows inserted outside the admin UI (in M1, every row).
    /// </summary>
    public Guid? ChangedByUserId { get; set; }

    public DateTimeOffset ChangedAt { get; set; }
}
