namespace Apollo.Domain.Tracking;

/// <summary>
/// Which branch of <see cref="TrackingRule"/> decides who is tracked on a platform (scope §5.1.1).
/// </summary>
public enum TrackingMode
{
    /// <summary>
    /// No tracking role has ever been configured: members with the Administrator permission are tracked.
    /// </summary>
    // TEMPORARY: removed by #62
    Bootstrap,

    /// <summary>A tracking role is configured and exists: exactly its holders are tracked.</summary>
    TrackingRole,

    /// <summary>The configured tracking role was deleted on the platform: nobody is tracked.</summary>
    TrackingRoleDeleted,

    /// <summary>A tracking role was configured before but has been removed: nobody is tracked.</summary>
    TrackingRoleCleared,
}
