using MudBlazor;
using MudBlazor.Services;

namespace Apollo.Web.Client.Tests;

/// <summary>
/// Stands in for MudBlazor's JS-backed viewport service, so tests can pick the breakpoint the drawer sees.
/// </summary>
public sealed class FakeBrowserViewportService(Breakpoint breakpoint) : IBrowserViewportService
{
    private readonly List<IBrowserViewportObserver> _observers = [];

    public Breakpoint Breakpoint { get; set; } = breakpoint;

    public ResizeOptions ResizeOptions { get; } = new();

    public Task SubscribeAsync(IBrowserViewportObserver observer, bool fireImmediately = true)
    {
        _observers.Add(observer);
        return fireImmediately ? observer.NotifyBrowserViewportChangeAsync(CreateArgs(isImmediate: true)) : Task.CompletedTask;
    }

    public Task SubscribeAsync(Guid observerId, Action<BrowserViewportEventArgs> lambda, ResizeOptions? options = null, bool fireImmediately = true)
    {
        if (fireImmediately)
        {
            lambda(CreateArgs(isImmediate: true));
        }

        return Task.CompletedTask;
    }

    public Task SubscribeAsync(Guid observerId, Func<BrowserViewportEventArgs, Task> lambda, ResizeOptions? options = null, bool fireImmediately = true) =>
        fireImmediately ? lambda(CreateArgs(isImmediate: true)) : Task.CompletedTask;

    public Task UnsubscribeAsync(IBrowserViewportObserver observer)
    {
        _observers.Remove(observer);
        return Task.CompletedTask;
    }

    public Task UnsubscribeAsync(Guid observerId) => Task.CompletedTask;

    public Task<bool> IsMediaQueryMatchAsync(string mediaQuery) => Task.FromResult(false);

    public Task<bool> IsBreakpointWithinWindowSizeAsync(Breakpoint breakpoint) => Task.FromResult(Breakpoint >= breakpoint);

    public Task<bool> IsBreakpointWithinReferenceSizeAsync(Breakpoint breakpoint, Breakpoint reference) => Task.FromResult(breakpoint == reference);

    public Task<Breakpoint> GetCurrentBreakpointAsync() => Task.FromResult(Breakpoint);

    public Task<BrowserWindowSize> GetCurrentBrowserWindowSizeAsync() => Task.FromResult(new BrowserWindowSize());

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private BrowserViewportEventArgs CreateArgs(bool isImmediate) =>
        new(Guid.Empty, new BrowserWindowSize(), Breakpoint, isImmediate);
}
