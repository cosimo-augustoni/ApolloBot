using Apollo.Platform.Abstractions.Events;

namespace Apollo.Platform.Abstractions.Dispatch;

/// <summary>
/// Delivers a platform event to every registered <see cref="IPlatformEventHandler{TEvent}"/> of its runtime type.
/// Called by the adapters and by <c>FakeChatPlatform</c>, so tests use the same path as production.
/// </summary>
public interface IPlatformEventDispatcher
{
    /// <summary>
    /// Runs the handlers one after another in one new DI scope per event. A failing handler is logged and does not
    /// stop the others; cancellation is not swallowed.
    /// </summary>
    Task DispatchAsync(PlatformEvent platformEvent, CancellationToken cancellationToken);
}
