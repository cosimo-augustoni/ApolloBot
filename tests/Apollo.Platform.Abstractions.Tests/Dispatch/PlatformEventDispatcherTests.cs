using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;

namespace Apollo.Platform.Abstractions.Tests.Dispatch;

public sealed class PlatformEventDispatcherTests
{
    private readonly HandlerLog _log = new();
    private FakeLogCollector _logs = null!;

    [Fact]
    public async Task DispatchAsync_WhenTwoHandlersAreRegistered_ShouldDeliverToEachExactlyOnce()
    {
        var dispatcher = BuildDispatcher(services => services
            .AddPlatformEventHandler<PlatformMessageCreated, FirstHandler>()
            .AddPlatformEventHandler<PlatformMessageCreated, SecondHandler>());
        var message = new PlatformMessageCreatedBuilder().Build();

        await dispatcher.DispatchAsync(message, TestContext.Current.CancellationToken);

        _log.Received.ShouldBe([(nameof(FirstHandler), message), (nameof(SecondHandler), message)]);
    }

    [Fact]
    public async Task DispatchAsync_WhenEventIsTypedAsBase_ShouldDeliverToHandlersOfRuntimeType()
    {
        var dispatcher = BuildDispatcher(services => services
            .AddPlatformEventHandler<PlatformMessageCreated, FirstHandler>());
        PlatformEvent message = new PlatformMessageCreatedBuilder().Build();

        await dispatcher.DispatchAsync(message, TestContext.Current.CancellationToken);

        _log.Received.ShouldBe([(nameof(FirstHandler), message)]);
    }

    [Fact]
    public async Task DispatchAsync_WhenNoHandlerIsRegistered_ShouldDoNothing()
    {
        var dispatcher = BuildDispatcher(services => services
            .AddPlatformEventHandler<PlatformMessageCreated, FirstHandler>());

        await dispatcher.DispatchAsync(new PlatformMessageDeletedBuilder().Build(), TestContext.Current.CancellationToken);

        _log.Received.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_WhenAHandlerThrows_ShouldLogAndStillDeliverToOtherHandlers()
    {
        var dispatcher = BuildDispatcher(services => services
            .AddPlatformEventHandler<PlatformMessageCreated, ThrowingHandler>()
            .AddPlatformEventHandler<PlatformMessageCreated, SecondHandler>());
        var message = new PlatformMessageCreatedBuilder().Build();

        await dispatcher.DispatchAsync(message, TestContext.Current.CancellationToken);

        _log.Received.ShouldBe([(nameof(SecondHandler), message)]);
        var error = _logs.LatestRecord;
        error.Level.ShouldBe(LogLevel.Error);
        error.Exception.ShouldBeOfType<InvalidOperationException>();
        error.Message.ShouldContain(nameof(ThrowingHandler));
        error.Message.ShouldContain(nameof(PlatformMessageCreated));
    }

    [Fact]
    public async Task DispatchAsync_WhenCancelled_ShouldThrowInsteadOfLogging()
    {
        var dispatcher = BuildDispatcher(services => services
            .AddPlatformEventHandler<PlatformMessageCreated, CancellingHandler>()
            .AddPlatformEventHandler<PlatformMessageCreated, SecondHandler>());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Should.ThrowAsync<OperationCanceledException>(
            () => dispatcher.DispatchAsync(new PlatformMessageCreatedBuilder().Build(), cancellation.Token));

        _log.Received.ShouldBeEmpty();
    }

    [Fact]
    public async Task DispatchAsync_WhenCalled_ShouldResolveHandlersFromOneScopePerEvent()
    {
        var dispatcher = BuildDispatcher(services => services
            .AddScoped<ScopeProbe>()
            .AddPlatformEventHandler<PlatformMessageCreated, ScopeRecordingHandler>()
            .AddPlatformEventHandler<PlatformMessageCreated, ScopeRecordingHandler>());

        await dispatcher.DispatchAsync(new PlatformMessageCreatedBuilder().Build(), TestContext.Current.CancellationToken);
        await dispatcher.DispatchAsync(new PlatformMessageCreatedBuilder().Build(), TestContext.Current.CancellationToken);

        _log.Scopes.Count.ShouldBe(4);
        _log.Scopes[0].ShouldBeSameAs(_log.Scopes[1]);
        _log.Scopes[2].ShouldBeSameAs(_log.Scopes[3]);
        _log.Scopes[0].ShouldNotBeSameAs(_log.Scopes[2]);
        _log.Scopes[0].IsDisposed.ShouldBeTrue();
    }

    private IPlatformEventDispatcher BuildDispatcher(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection()
            .AddFakeLogging()
            .AddSingleton(_log)
            .AddPlatformEventDispatcher();
        configure(services);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _logs = provider.GetFakeLogCollector();
        return provider.GetRequiredService<IPlatformEventDispatcher>();
    }

    private sealed class HandlerLog
    {
        public List<(string Handler, PlatformEvent Event)> Received { get; } = [];

        public List<ScopeProbe> Scopes { get; } = [];
    }

    private sealed class ScopeProbe : IDisposable
    {
        public bool IsDisposed { get; private set; }

        public void Dispose() => IsDisposed = true;
    }

    private sealed class FirstHandler(HandlerLog log) : IPlatformEventHandler<PlatformMessageCreated>
    {
        public Task HandleAsync(PlatformMessageCreated platformEvent, CancellationToken cancellationToken)
        {
            log.Received.Add((nameof(FirstHandler), platformEvent));
            return Task.CompletedTask;
        }
    }

    private sealed class SecondHandler(HandlerLog log) : IPlatformEventHandler<PlatformMessageCreated>
    {
        public Task HandleAsync(PlatformMessageCreated platformEvent, CancellationToken cancellationToken)
        {
            log.Received.Add((nameof(SecondHandler), platformEvent));
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingHandler : IPlatformEventHandler<PlatformMessageCreated>
    {
        public Task HandleAsync(PlatformMessageCreated platformEvent, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Handler failed.");
    }

    private sealed class CancellingHandler : IPlatformEventHandler<PlatformMessageCreated>
    {
        public Task HandleAsync(PlatformMessageCreated platformEvent, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.CompletedTask;
        }
    }

    private sealed class ScopeRecordingHandler(HandlerLog log, ScopeProbe probe) : IPlatformEventHandler<PlatformMessageCreated>
    {
        public Task HandleAsync(PlatformMessageCreated platformEvent, CancellationToken cancellationToken)
        {
            log.Scopes.Add(probe);
            return Task.CompletedTask;
        }
    }
}
