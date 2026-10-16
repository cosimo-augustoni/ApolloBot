using System.Collections.Concurrent;
using Apollo.Platform.Abstractions.Events;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Apollo.Platform.Abstractions.Dispatch;

internal sealed partial class PlatformEventDispatcher(
    IServiceScopeFactory scopeFactory,
    ILogger<PlatformEventDispatcher> logger) : IPlatformEventDispatcher
{
    private static readonly ConcurrentDictionary<Type, HandlerInvoker> _invokers = new();

    public async Task DispatchAsync(PlatformEvent platformEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(platformEvent);
        var invoker = _invokers.GetOrAdd(platformEvent.GetType(), HandlerInvoker.Create);

        await using var scope = scopeFactory.CreateAsyncScope();
        foreach (var handler in invoker.ResolveHandlers(scope.ServiceProvider))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await invoker.InvokeAsync(handler, platformEvent, cancellationToken);
            }
            catch (Exception exception) when (!(exception is OperationCanceledException && cancellationToken.IsCancellationRequested))
            {
                LogHandlerFailed(exception, handler.GetType().Name, platformEvent.GetType().Name);
            }
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Platform event handler {Handler} failed for {EventType}")]
    private partial void LogHandlerFailed(Exception exception, string handler, string eventType);

    /// <summary>
    /// Bridges the runtime event type to the generic handler interface, so callers can dispatch a
    /// <see cref="PlatformEvent"/> without knowing its concrete type.
    /// </summary>
    private abstract class HandlerInvoker
    {
        public static HandlerInvoker Create(Type eventType) =>
            (HandlerInvoker)Activator.CreateInstance(typeof(HandlerInvoker<>).MakeGenericType(eventType))!;

        public abstract IEnumerable<object> ResolveHandlers(IServiceProvider services);

        public abstract Task InvokeAsync(object handler, PlatformEvent platformEvent, CancellationToken cancellationToken);
    }

    private sealed class HandlerInvoker<TEvent> : HandlerInvoker
        where TEvent : PlatformEvent
    {
        public override IEnumerable<object> ResolveHandlers(IServiceProvider services) =>
            services.GetServices<IPlatformEventHandler<TEvent>>().Cast<object>();

        public override Task InvokeAsync(object handler, PlatformEvent platformEvent, CancellationToken cancellationToken) =>
            ((IPlatformEventHandler<TEvent>)handler).HandleAsync((TEvent)platformEvent, cancellationToken);
    }
}
