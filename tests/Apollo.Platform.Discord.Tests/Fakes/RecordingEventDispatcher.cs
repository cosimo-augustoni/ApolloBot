using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Platform.Discord.Tests.Fakes;

/// <summary>
/// Collects what the adapter dispatches, so mapping tests assert on the neutral events.
/// </summary>
internal sealed class RecordingEventDispatcher : IPlatformEventDispatcher
{
    private readonly List<PlatformEvent> _events = [];

    public IReadOnlyList<PlatformEvent> Events => _events;

    public Task DispatchAsync(PlatformEvent platformEvent, CancellationToken cancellationToken)
    {
        _events.Add(platformEvent);
        return Task.CompletedTask;
    }
}
