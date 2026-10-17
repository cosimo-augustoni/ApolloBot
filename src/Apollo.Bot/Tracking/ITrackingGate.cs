namespace Apollo.Bot.Tracking;

/// <summary>
/// Whether a user's activity is recorded (scope §5.1.1): the only gate. Answers synchronously from an in-memory snapshot,
/// so recorders never query the database per event. The snapshot follows <c>role_settings</c>, <c>member_roles</c> and
/// <c>guild_members</c>: live for member changes, within 30 s for role setting changes.
/// </summary>
/// <remarks>
/// <para>
/// Until the first full guild sync after startup nobody is tracked and <see cref="ITrackingView.Mode"/> is
/// <see langword="null"/>. The gate covers the one guild the bot records (scope §4.3).
/// </para>
/// <para>
/// Each member of <see cref="ITrackingView"/> answers from the latest snapshot. Code that needs several answers to agree
/// (e.g. the tracking role and whether the member holds it) reads <see cref="Current"/> once instead.
/// </para>
/// </remarks>
public interface ITrackingGate : ITrackingView
{
    /// <summary>
    /// The latest snapshot as an immutable view: it keeps its answers when the gate refreshes.
    /// </summary>
    ITrackingView Current { get; }
}
