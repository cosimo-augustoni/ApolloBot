using Apollo.Domain.Tracking;
using Apollo.Platform.Abstractions;

namespace Apollo.Bot.Tracking;

/// <summary>
/// Whether a user's activity is recorded (scope §5.1.1): the only gate. Answers synchronously from an in-memory snapshot,
/// so recorders never query the database per event. The snapshot follows <c>role_settings</c>, <c>member_roles</c> and
/// <c>guild_members</c>: live for member changes, within 30 s for role setting changes.
/// </summary>
/// <remarks>
/// Until the first full guild sync after startup nobody is tracked and <see cref="Mode"/> is <see langword="null"/>.
/// The gate covers the one guild the bot records (scope §4.3).
/// </remarks>
public interface ITrackingGate
{
    /// <summary>
    /// The current mode; <see langword="null"/> before the first snapshot.
    /// </summary>
    TrackingMode? Mode { get; }

    /// <summary>
    /// Whether the user, by internal ID, is tracked right now.
    /// </summary>
    bool IsTracked(Guid userId);

    /// <summary>
    /// The internal ID of a referenced user if that user is tracked, otherwise <see langword="null"/>. Never touches the
    /// database and never creates a row, so references to untracked users (<c>reply_to_user_id</c>,
    /// <c>message_author_id</c>) are stored as <c>NULL</c> (scope §5.1.1).
    /// </summary>
    Guid? TryGetTrackedUserId(PlatformRef user);
}
