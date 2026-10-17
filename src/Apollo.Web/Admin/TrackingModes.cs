using Apollo.Domain.Tracking;
using Apollo.Web.Client.Contracts;

namespace Apollo.Web.Admin;

public static class TrackingModes
{
    /// <summary>The tracking mode as the admin API reports it (#33, #32).</summary>
    public static RoleSettingsMode ToDto(this TrackingMode mode) => mode switch
    {
        TrackingMode.NoTrackingRole => RoleSettingsMode.NoTrackingRole,
        TrackingMode.TrackingRole => RoleSettingsMode.Active,
        TrackingMode.TrackingRoleDeleted => RoleSettingsMode.TrackingRoleDeleted,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown tracking mode."),
    };
}
