namespace Apollo.Platform.Abstractions.Dispatch;

/// <summary>
/// When <see cref="IPlatformEventDispatcher"/> last received an event, for the bot heartbeat (#24).
/// </summary>
public interface IPlatformEventActivity
{
    /// <summary>
    /// When the latest guild event was dispatched (the dispatcher's clock, not the event's <c>OccurredAt</c>);
    /// <see langword="null"/> until the first one. Connection lifecycle events
    /// (<see cref="Events.PlatformConnectionStateChanged"/>, <see cref="Events.PlatformGuildAvailable"/>) do not count.
    /// </summary>
    DateTimeOffset? LastEventAt { get; }
}
