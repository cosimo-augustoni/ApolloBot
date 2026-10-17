using Apollo.Bot.Tracking;

namespace Apollo.Bot.Tests.Tracking;

/// <summary>
/// Fails like a handler whose own timeout ran out, while the gate itself is not cancelled.
/// </summary>
internal sealed class CancellingLifecycleHandler : ITrackingLifecycleHandler
{
    public Task TrackingStartedAsync(TrackedUser user, CancellationToken cancellationToken) =>
        throw new TaskCanceledException("Simulated: the handler timed out.");

    public Task TrackingStoppedAsync(TrackedUser user, CancellationToken cancellationToken) =>
        throw new TaskCanceledException("Simulated: the handler timed out.");
}
