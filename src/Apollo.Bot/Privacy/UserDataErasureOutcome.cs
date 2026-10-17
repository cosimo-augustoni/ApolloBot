namespace Apollo.Bot.Privacy;

/// <summary>
/// What <see cref="IUserDataEraser.EraseAsync"/> did.
/// </summary>
public enum UserDataErasureOutcome
{
    /// <summary>
    /// Nothing was deleted: the deletion failed and was rolled back. Tracking is as before.
    /// </summary>
    Failed,

    /// <summary>
    /// The data was deleted and the tracking role removed.
    /// </summary>
    DeletedAndRoleRemoved,

    /// <summary>
    /// The data was deleted; the member did not hold the tracking role, so none was removed.
    /// </summary>
    Deleted,

    /// <summary>
    /// The data was deleted, but the platform refused to remove the tracking role: the member is still tracked.
    /// </summary>
    DeletedButRoleKept,

    /// <summary>
    /// The data was deleted; no tracking role is set up (deleted, cleared, or before the first guild sync), so none was
    /// removed and nobody is tracked.
    /// </summary>
    DeletedNotSetUp,

    /// <summary>
    /// The data was deleted in bootstrap mode: no role was removed, and an Administrator stays tracked. TEMPORARY:
    /// removed by #62.
    /// </summary>
    DeletedInBootstrapMode,
}
