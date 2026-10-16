using Apollo.Bot.Tracking;

namespace Apollo.Bot.Tests.Tracking;

internal sealed class ThrowingLifecycleHandler : ITrackingLifecycleHandler
{
    public Task TrackingStartedAsync(TrackedUser user, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Hook failed.");

    public Task TrackingStoppedAsync(TrackedUser user, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("Hook failed.");
}
