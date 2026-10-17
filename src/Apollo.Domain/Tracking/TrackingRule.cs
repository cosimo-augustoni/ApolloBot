namespace Apollo.Domain.Tracking;

/// <summary>
/// Who is recorded (scope §5.1.1, D11): exactly the holders of the configured tracking role. This is the only gate: no
/// other code decides tracking, and no permission (Administrator included) makes a member tracked.
/// </summary>
public static class TrackingRule
{
    public static TrackingMode ModeOf(TrackingSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings switch
        {
            { TrackingRoleId: null } => TrackingMode.NoTrackingRole,
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
            _ => false,
        };
    }
}
