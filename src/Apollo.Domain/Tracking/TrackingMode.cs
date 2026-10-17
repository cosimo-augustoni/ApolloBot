namespace Apollo.Domain.Tracking;

/// <summary>
/// Which branch of <see cref="TrackingRule"/> decides who is tracked on a platform (scope §5.1.1).
/// </summary>
public enum TrackingMode
{
    /// <summary>No tracking role is configured: nobody is tracked, Administrators included.</summary>
    NoTrackingRole,

    /// <summary>A tracking role is configured and exists: exactly its holders are tracked.</summary>
    TrackingRole,

    /// <summary>The configured tracking role was deleted on the platform: nobody is tracked.</summary>
    TrackingRoleDeleted,
}
