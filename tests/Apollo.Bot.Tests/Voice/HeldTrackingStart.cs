using Apollo.Bot.Tracking;
using Apollo.Platform.Abstractions;

namespace Apollo.Bot.Tests.Voice;

/// <summary>
/// Holds the gate's <c>TrackingStartedAsync</c> call for <see cref="User"/> before it reaches the voice recorder (its
/// handler is registered first), so a test can deliver a voice event in the window after the gate published its
/// snapshot and before the recorder's hook ran.
/// </summary>
internal sealed class HeldTrackingStart(PlatformRef user)
{
    private readonly TaskCompletionSource _entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PlatformRef User { get; } = user;

    /// <summary>
    /// Completes once the gate called the hook for <see cref="User"/>.
    /// </summary>
    public Task Entered => _entered.Task;

    public void Release() => _released.TrySetResult();

    public async Task WaitAsync(TrackedUser user, CancellationToken cancellationToken)
    {
        if (user.User != User)
        {
            return;
        }

        _entered.TrySetResult();
        await _released.Task.WaitAsync(cancellationToken);
    }

    /// <summary>
    /// The handler that waits on the shared <see cref="HeldTrackingStart"/>.
    /// </summary>
    internal sealed class Handler(HeldTrackingStart hold) : ITrackingLifecycleHandler
    {
        public Task TrackingStartedAsync(TrackedUser user, CancellationToken cancellationToken) => hold.WaitAsync(user, cancellationToken);

        public Task TrackingStoppedAsync(TrackedUser user, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
