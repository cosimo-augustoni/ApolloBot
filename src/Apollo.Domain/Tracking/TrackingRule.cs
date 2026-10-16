namespace Apollo.Domain.Tracking;

/// <summary>
/// Who is recorded (scope §5.1.1, D11). This is the only gate: no other code decides tracking, and nothing else checks
/// the Administrator permission for it.
/// </summary>
/// <remarks>
/// The Administrator bootstrap lives here and only here (owner decision on #14): the <see cref="TrackingMode.Bootstrap"/>
/// branch of <see cref="ModeOf"/> and of <see cref="IsTracked"/>. #62 deletes both once the admin UI (#33) can set the
/// tracking role.
/// </remarks>
public static class TrackingRule
{
    public static TrackingMode ModeOf(TrackingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings switch
        {
            { TrackingRoleId: null, TrackingRoleEverConfigured: false } => TrackingMode.Bootstrap, // TEMPORARY: removed by #62
            { TrackingRoleId: null } => TrackingMode.TrackingRoleCleared,
            { TrackingRoleDeleted: true } => TrackingMode.TrackingRoleDeleted,
            _ => TrackingMode.TrackingRole,
        };
    }

    public static bool IsTracked(TrackingSettings settings, TrackingMember member)
    {
        ArgumentNullException.ThrowIfNull(member);
        if (!member.IsCurrentMember)
        {
            return false;
        }

        return ModeOf(settings) switch
        {
            TrackingMode.TrackingRole => member.RoleIds.Contains(settings.TrackingRoleId!.Value),
            TrackingMode.Bootstrap => member.IsAdministrator, // TEMPORARY: removed by #62
            _ => false,
        };
    }
}
