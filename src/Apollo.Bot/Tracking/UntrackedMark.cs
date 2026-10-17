namespace Apollo.Bot.Tracking;

/// <summary>
/// What <see cref="TrackingGate.MarkUntrackedAsync"/> did.
/// </summary>
internal enum UntrackedMark
{
    /// <summary>
    /// The user was not tracked, so nothing was marked.
    /// </summary>
    NotTracked,

    /// <summary>
    /// This call marked the user; its caller releases the mark unless the role removal ends it.
    /// </summary>
    Added,

    /// <summary>
    /// Another deletion had already marked the user and still owns the mark. The gate keeps a mark only while the rule
    /// tracks the user, so they held the tracking role when it was last synced.
    /// </summary>
    AlreadyMarked,
}
