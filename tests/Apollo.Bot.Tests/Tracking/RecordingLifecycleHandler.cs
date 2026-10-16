using Apollo.Bot.Tracking;

namespace Apollo.Bot.Tests.Tracking;

internal sealed class RecordingLifecycleHandler(LifecycleLog log) : ITrackingLifecycleHandler
{
    public Task TrackingStartedAsync(TrackedUser user, CancellationToken cancellationToken)
    {
        log.Add("started", user);
        return Task.CompletedTask;
    }

    public Task TrackingStoppedAsync(TrackedUser user, CancellationToken cancellationToken)
    {
        log.Add("stopped", user);
        return Task.CompletedTask;
    }
}
