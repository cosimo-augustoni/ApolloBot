using Apollo.Platform.Abstractions.Events;

namespace Apollo.Platform.Abstractions.Dispatch;

/// <summary>
/// Consumes one kind of platform event. Register with
/// <see cref="PlatformEventServiceCollectionExtensions.AddPlatformEventHandler{TEvent, THandler}"/>; handlers are
/// scoped, so they may depend on a scoped <c>DbContext</c>.
/// </summary>
public interface IPlatformEventHandler<in TEvent>
    where TEvent : PlatformEvent
{
    Task HandleAsync(TEvent platformEvent, CancellationToken cancellationToken);
}
