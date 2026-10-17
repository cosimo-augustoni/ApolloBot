namespace Apollo.Bot.Privacy;

/// <summary>
/// What <see cref="IUserDataEraser.EraseAsync"/> did.
/// </summary>
public enum UserDataErasureOutcome
{
    /// <summary>
    /// Nothing was deleted: the deletion failed and was rolled back. The member's open voice and presence sessions and
    /// their tracking period were already closed; if the rule still tracks them (they hold the tracking role), they are
    /// tracked again from a new period.
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
    /// The data was deleted; no tracking role is set up (none set, deleted, or before the first guild sync), so none was
    /// removed and nobody is tracked.
    /// </summary>
    DeletedNotSetUp,
}
