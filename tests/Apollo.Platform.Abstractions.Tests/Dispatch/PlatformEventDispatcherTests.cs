using Apollo.Platform.Abstractions.Diagnostics;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Testing;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Platform.Abstractions.Tests.Dispatch;

public sealed class PlatformEventDispatcherTests
{
    private readonly HandlerLog _log = new();
    private readonly FakeTimeProvider _time = TestTime.CreateProvider();
    private FakeLogCollector _logs = null!;
    private IServiceProvider _provider = null!;

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
    public async Task DispatchAsync_WhenCalled_ShouldResolveEachHandlerInAScopeOfItsOwn()
    {
        var dispatcher = BuildDispatcher(services => services
            .AddScoped<ScopeProbe>()
            .AddPlatformEventHandler<PlatformMessageCreated, ScopeRecordingHandler>()
            .AddPlatformEventHandler<PlatformMessageCreated, ScopeRecordingHandler>());

        await dispatcher.DispatchAsync(new PlatformMessageCreatedBuilder().Build(), TestContext.Current.CancellationToken);
        await dispatcher.DispatchAsync(new PlatformMessageCreatedBuilder().Build(), TestContext.Current.CancellationToken);

        _log.Scopes.Count.ShouldBe(4);
        _log.Scopes.Distinct().Count().ShouldBe(4);
        _log.Scopes.ShouldAllBe(scope => scope.IsDisposed);
    }

    /// <summary>
    /// A handler that fails leaves its unit of work (e.g. a <c>DbContext</c> with pending changes) behind; the next
    /// handler of the same event must not save it.
    /// </summary>
    [Fact]
    public async Task DispatchAsync_WhenAHandlerFailsWithPendingWork_ShouldNotLetTheNextHandlerSaveIt()
    {
        var dispatcher = BuildDispatcher(services => services
            .AddScoped<UnitOfWork>()
            .AddPlatformEventHandler<PlatformMessageCreated, FailingWriter>()
            .AddPlatformEventHandler<PlatformMessageCreated, SavingWriter>());

        await dispatcher.DispatchAsync(new PlatformMessageCreatedBuilder().Build(), TestContext.Current.CancellationToken);

        _log.Saved.ShouldBe([nameof(SavingWriter)]);
    }

    [Fact]
    public void LastEventAt_WhenNothingWasDispatched_ShouldBeNull()
    {
        BuildDispatcher(_ => { });

        _provider.GetRequiredService<IPlatformEventActivity>().LastEventAt.ShouldBeNull();
    }

    [Fact]
    public async Task LastEventAt_WhenAnEventWasDispatchedAtTAndNoneSince_ShouldBeT()
    {
        var dispatcher = BuildDispatcher(_ => { });
        var dispatchedAt = _time.GetUtcNow();

        await dispatcher.DispatchAsync(new PlatformMessageCreatedBuilder().Build(), TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(5));

        _provider.GetRequiredService<IPlatformEventActivity>().LastEventAt.ShouldBe(dispatchedAt);
    }

    [Fact]
    public async Task LastEventAt_WhenEventsWereDispatchedAtDifferentTimes_ShouldBeTheLatest()
    {
        var dispatcher = BuildDispatcher(_ => { });

        await dispatcher.DispatchAsync(new PlatformMessageCreatedBuilder().Build(), TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromSeconds(10));
        await dispatcher.DispatchAsync(new PlatformMessageDeletedBuilder().Build(), TestContext.Current.CancellationToken);

        _provider.GetRequiredService<IPlatformEventActivity>().LastEventAt.ShouldBe(TestTime.Now.AddSeconds(10));
    }

    [Fact]
    public async Task LastEventAt_WhenOnlyConnectionStateChangesFollow_ShouldStayAtTheLastGuildEvent()
    {
        var dispatcher = BuildDispatcher(_ => { });
        await dispatcher.DispatchAsync(new PlatformMessageCreatedBuilder().Build(), TestContext.Current.CancellationToken);

        _time.Advance(TimeSpan.FromMinutes(1));
        await dispatcher.DispatchAsync(new PlatformConnectionStateChangedBuilder().Build(), TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromMinutes(1));
        await dispatcher.DispatchAsync(new PlatformGuildAvailableBuilder().Build(), TestContext.Current.CancellationToken);

        _provider.GetRequiredService<IPlatformEventActivity>().LastEventAt.ShouldBe(TestTime.Now);
    }

    [Fact]
    public async Task LastEventAt_WhenOnlyConnectionEventsWereDispatched_ShouldBeNull()
    {
        var dispatcher = BuildDispatcher(_ => { });

        await dispatcher.DispatchAsync(new PlatformConnectionStateChangedBuilder().Build(), TestContext.Current.CancellationToken);
        await dispatcher.DispatchAsync(new PlatformGuildAvailableBuilder().Build(), TestContext.Current.CancellationToken);

        _provider.GetRequiredService<IPlatformEventActivity>().LastEventAt.ShouldBeNull();
    }

    [Fact]
    public void AddPlatformEventDispatcher_WithoutAHost_ShouldRegisterEverythingItNeeds()
    {
        using var provider = new ServiceCollection()
            .AddLogging()
            .AddPlatformEventDispatcher()
            .BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        provider.GetRequiredService<RecordedEventMetrics>().ShouldNotBeNull();
    }

    private IPlatformEventDispatcher BuildDispatcher(Action<IServiceCollection> configure)
    {
        var services = new ServiceCollection()
            .AddFakeLogging()
            .AddSingleton(_log)
            .AddSingleton<TimeProvider>(_time)
            .AddPlatformEventDispatcher();
        configure(services);
        var provider = services.BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true });
        _provider = provider;
        _logs = provider.GetFakeLogCollector();
        return provider.GetRequiredService<IPlatformEventDispatcher>();
    }

    private sealed class HandlerLog
    {
        public List<(string Handler, PlatformEvent Event)> Received { get; } = [];

        public List<ScopeProbe> Scopes { get; } = [];

        public List<string> Saved { get; } = [];
    }

    /// <summary>
    /// A scoped unit of work, like a <c>DbContext</c>: <see cref="Save"/> writes everything added so far.
    /// </summary>
    private sealed class UnitOfWork(HandlerLog log)
    {
        private readonly List<string> _pending = [];

        public void Add(string item) => _pending.Add(item);

        public void Save()
        {
            log.Saved.AddRange(_pending);
            _pending.Clear();
        }
    }

    private sealed class FailingWriter(UnitOfWork work) : IPlatformEventHandler<PlatformMessageCreated>
    {
        public Task HandleAsync(PlatformMessageCreated platformEvent, CancellationToken cancellationToken)
        {
            work.Add(nameof(FailingWriter));
            throw new InvalidOperationException("Save failed.");
        }
    }

    private sealed class SavingWriter(UnitOfWork work) : IPlatformEventHandler<PlatformMessageCreated>
    {
        public Task HandleAsync(PlatformMessageCreated platformEvent, CancellationToken cancellationToken)
        {
            work.Add(nameof(SavingWriter));
            work.Save();
            return Task.CompletedTask;
        }
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
