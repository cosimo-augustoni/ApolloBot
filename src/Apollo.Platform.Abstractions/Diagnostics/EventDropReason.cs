namespace Apollo.Platform.Abstractions.Diagnostics;

/// <summary>
/// Why an event was not recorded; the <c>reason</c> tag of <see cref="RecordedEventMetrics.DroppedCounter"/>.
/// </summary>
public enum EventDropReason
{
    /// <summary>
    /// The adapter's single-guild filter dropped it (scope §4.3): another guild, or no guild at all (DMs).
    /// </summary>
    OtherGuild,

    /// <summary>
    /// The tracking gate dropped it: the user is not tracked (scope §5.1.1).
    /// </summary>
    Untracked,
}
