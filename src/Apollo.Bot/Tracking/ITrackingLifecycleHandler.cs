namespace Apollo.Bot.Tracking;

/// <summary>
/// Reacts to a user gaining or losing tracking, e.g. to open or close voice and presence sessions (#16, #19). Register
/// with <see cref="TrackingServiceCollectionExtensions.AddTrackingLifecycleHandler{THandler}"/>; handlers are scoped.
/// </summary>
/// <remarks>
/// Called after the gate's snapshot and <c>tracking_periods</c> reflect the change, one user at a time and never
/// concurrently. <see cref="TrackingStartedAsync"/> also runs for every tracked user after the first sync on startup, so
/// session recorders can reconcile. A failing handler is logged and does not stop the others.
/// </remarks>
public interface ITrackingLifecycleHandler
{
    Task TrackingStartedAsync(TrackedUser user, CancellationToken cancellationToken);

    Task TrackingStoppedAsync(TrackedUser user, CancellationToken cancellationToken);
}
