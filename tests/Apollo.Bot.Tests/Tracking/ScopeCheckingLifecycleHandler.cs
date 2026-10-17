using Apollo.Bot.Tracking;

namespace Apollo.Bot.Tests.Tracking;

/// <summary>
/// Dirties its scope and fails on the first call, and logs whether any call found its scope already dirty.
/// </summary>
internal sealed class ScopeCheckingLifecycleHandler(ScopeProbe probe, ScopeCheckLog log) : ITrackingLifecycleHandler
{
    public Task TrackingStartedAsync(TrackedUser user, CancellationToken cancellationToken) => CheckAsync(user);

    public Task TrackingStoppedAsync(TrackedUser user, CancellationToken cancellationToken) => CheckAsync(user);

    private Task CheckAsync(TrackedUser user)
    {
        var first = log.Record(user.UserId, sawDirtyScope: probe.Dirty);
        probe.Dirty = true;
        return first ? throw new InvalidOperationException("Hook failed and left its scope dirty.") : Task.CompletedTask;
    }
}
