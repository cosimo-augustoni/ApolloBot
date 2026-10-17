using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;

namespace Apollo.Bot.Tracking;

/// <summary>
/// The tracking gate's answers (scope §5.1.1). <see cref="ITrackingGate.Current"/> returns one immutable view, so
/// several answers read from it always belong together, even if the gate refreshes in between.
/// </summary>
public interface ITrackingView
{
    /// <summary>
    /// The mode; <see langword="null"/> before the first snapshot.
    /// </summary>
    TrackingMode? Mode { get; }

    /// <summary>
    /// The tracking role on the bot's platform while one is configured and exists (<see cref="Mode"/> is
    /// <see cref="TrackingMode.TrackingRole"/>); <see langword="null"/> otherwise, which the tracking commands treat as
    /// "not set up" (scope §5.4).
    /// </summary>
    PlatformRef? TrackingRole { get; }

    /// <summary>
    /// Whether the user, by internal ID, is tracked.
    /// </summary>
    bool IsTracked(Guid userId);

    /// <summary>
    /// The internal ID of a referenced user if that user is tracked, otherwise <see langword="null"/>. Never touches the
    /// database and never creates a row, so references to untracked users (<c>reply_to_user_id</c>,
    /// <c>message_author_id</c>) are stored as <c>NULL</c> (scope §5.1.1).
    /// </summary>
    Guid? TryGetTrackedUserId(PlatformRef user);
}
