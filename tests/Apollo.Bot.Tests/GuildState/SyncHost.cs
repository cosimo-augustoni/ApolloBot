using Apollo.Bot.GuildState;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Bot.Tests.GuildState;

/// <summary>
/// The guild state sync as the bot wires it (<see cref="GuildStateServiceCollectionExtensions.AddGuildStateSync"/>),
/// on the shared Timescale database with the runtime context registration
/// (<see cref="TimescaleFixture.CreateApplicationBuilder"/>) and behind a <see cref="FakeChatPlatform"/>. Each host has
/// its own <see cref="PlatformIdCache"/>, because Respawn empties the tables between tests.
/// </summary>
internal sealed class SyncHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    public SyncHost(TimescaleFixture fixture)
    {
        var services = fixture.CreateApplicationBuilder().Services;
        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton<IHostApplicationLifetime>(Lifetime);
        services.AddPlatformEventDispatcher();
        services.AddSingleton(serviceProvider => new FakeChatPlatform(
            serviceProvider.GetRequiredService<IPlatformEventDispatcher>(),
            serviceProvider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IChatPlatform>(serviceProvider => serviceProvider.GetRequiredService<FakeChatPlatform>());
        services.AddGuildStateSync();
        services.AddSingleton(Notifications);
        services.AddGuildStateNotificationHandler<MemberAccessStateChanged, RecordingNotificationHandler>();
        services.AddGuildStateNotificationHandler<GuildStateSynchronized, RecordingNotificationHandler>();
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public FakeChatPlatform Platform => _services.GetRequiredService<FakeChatPlatform>();

    /// <summary>
    /// The clock of the host; advance it to let the sync's retry delays elapse.
    /// </summary>
    public FakeTimeProvider Time { get; } = TestTime.CreateProvider();

    public NotificationLog Notifications { get; } = new();

    /// <summary>
    /// The host lifetime; <see cref="TestApplicationLifetime.StopApplication"/> simulates the bot shutting down.
    /// </summary>
    public TestApplicationLifetime Lifetime { get; } = new();

    /// <summary>
    /// Waits (on the wall clock, briefly) until the platform was asked for the member list <paramref name="count"/> times.
    /// </summary>
    public async Task WaitForMemberListingsAsync(int count)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (Platform.MemberListings < count)
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Expected {count} member listings, saw {Platform.MemberListings}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        }
    }

    public Task RaiseAsync(PlatformEvent platformEvent) => Platform.RaiseAsync(platformEvent, TestContext.Current.CancellationToken);

    /// <summary>
    /// Seeds the platform with the snapshot's members (the sync takes members from the full member list) and raises it.
    /// </summary>
    public async Task SyncAsync(PlatformGuildAvailable snapshot)
    {
        foreach (var member in snapshot.Members)
        {
            Platform.WithMember(member);
        }

        await RaiseAsync(snapshot);
    }

    /// <summary>
    /// Raises <paramref name="snapshot"/> as is (members come only from <see cref="Platform"/>) and advances the clock
    /// until the sync, retries included, has finished.
    /// </summary>
    public Task RaiseAndWaitAsync(PlatformGuildAvailable snapshot) =>
        Time.AdvanceUntilCompletedAsync(RaiseAsync(snapshot), TimeSpan.FromSeconds(10));

    /// <summary>
    /// Runs <paramref name="action"/> with a scoped context and resolver, e.g. to seed rows with the
    /// <c>Apollo.Testing.Data</c> builders.
    /// </summary>
    public async Task<T> InScopeAsync<T>(Func<ApolloDbContext, IPlatformIdResolver, Task<T>> action)
    {
        await using var scope = _services.CreateAsyncScope();
        return await action(
            scope.ServiceProvider.GetRequiredService<ApolloDbContext>(),
            scope.ServiceProvider.GetRequiredService<IPlatformIdResolver>());
    }

    public Task<Guid> UserIdAsync(PlatformRef user) =>
        InScopeAsync(async (_, ids) => await ids.TryGetUserAsync(user, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException($"{user} was never resolved."));

    public Task<Guid> GuildIdAsync() =>
        InScopeAsync(async (_, ids) => await ids.TryGetGuildAsync(TestPlatform.Guild, TestContext.Current.CancellationToken)
            ?? throw new InvalidOperationException("The guild was never resolved."));

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        Lifetime.Dispose();
    }
}
