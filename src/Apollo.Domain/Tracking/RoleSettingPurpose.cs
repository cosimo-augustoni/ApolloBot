namespace Apollo.Domain.Tracking;

/// <summary>
/// What a role setting is for (scope §5.1.2). Admin access is never a setting (D8), so there is no Administrator purpose.
/// </summary>
public enum RoleSettingPurpose
{
    /// <summary>The one role per platform whose holders are recorded (§5.1.1).</summary>
    Tracking,

    /// <summary>Roles that grant access to the web app (§6.2, M4). Only stored for now.</summary>
    MemberAccess,
}
