using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;

namespace Apollo.Data.Tracking;

/// <summary>
/// One entry of the append-only <c>role_setting_changes</c> audit log, written by a trigger on <c>role_settings</c>.
/// It is the source of the admin history (#33) and tells whether a tracking role was ever configured, which ends the
/// Administrator bootstrap for good (scope §5.1.1; that check is removed by #62).
/// </summary>
public sealed class RoleSettingChange
{
    public long Id { get; init; }

    public Guid GuildId { get; init; }

    public PlatformKind Platform { get; init; }

    public RoleSettingPurpose Purpose { get; init; }

    public Guid RoleId { get; init; }

    public RoleSettingAction Action { get; init; }

    /// <summary>
    /// Who made the change: the setting's author for <see cref="RoleSettingAction.Added"/>; always
    /// <see langword="null"/> for <see cref="RoleSettingAction.Removed"/>, since a deleted row names no author.
    /// </summary>
    public Guid? ChangedByUserId { get; init; }

    public DateTimeOffset ChangedAt { get; init; }
}
