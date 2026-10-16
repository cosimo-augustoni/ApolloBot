using Apollo.Bot.GuildState;
using Apollo.Bot.Tests.GuildState;
using Apollo.Bot.Tracking;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Data.Tracking;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using CmdScale.EntityFrameworkCore.TimescaleDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Bot.Tests.Tracking;

/// <summary>
/// The guild state sync and the tracking gate as the bot wires them, on the shared Timescale database and behind a
/// <see cref="FakeChatPlatform"/>, with a <see cref="RecordingLifecycleHandler"/> (registered after any <c>configure</c> registrations) and fake
/// logging. The reloader runs only
/// after <see cref="StartReloaderAsync"/>.
/// </summary>
internal sealed class TrackingHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;
    private IHostedService? _reloader;

    public TrackingHost(TimescaleFixture fixture, Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddFakeLogging();
        services.AddSingleton<TimeProvider>(Time);
        services.AddSingleton<IHostApplicationLifetime>(Lifetime);
        services.AddDbContext<ApolloDbContext>(options => options.UseNpgsql(fixture.ConnectionString).UseTimescaleDb());
        services.AddSingleton<PlatformIdCache>();
        services.AddScoped<IPlatformIdResolver, PlatformIdResolver>();
        services.AddPlatformEventDispatcher();
        services.AddSingleton(serviceProvider => new FakeChatPlatform(
            serviceProvider.GetRequiredService<IPlatformEventDispatcher>(),
            serviceProvider.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IChatPlatform>(serviceProvider => serviceProvider.GetRequiredService<FakeChatPlatform>());
        services.AddGuildStateSync();
        services.AddTrackingGate();
        services.AddSingleton(Lifecycle);
        configure?.Invoke(services);
        services.AddTrackingLifecycleHandler<RecordingLifecycleHandler>();
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public FakeChatPlatform Platform => _services.GetRequiredService<FakeChatPlatform>();

    public ITrackingGate Gate => _services.GetRequiredService<ITrackingGate>();

    public FakeTimeProvider Time { get; } = TestTime.CreateProvider();

    public LifecycleLog Lifecycle { get; } = new();

    public FakeLogCollector Logs => _services.GetFakeLogCollector();

    public TestApplicationLifetime Lifetime { get; } = new();

    public Task RaiseAsync(PlatformEvent platformEvent) => Platform.RaiseAsync(platformEvent, TestContext.Current.CancellationToken);

    /// <summary>
    /// Seeds the platform with the snapshot's members and raises it: the full guild sync, as on startup.
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
    /// Starts the <see cref="TrackingGateReloader"/>; its first reload is due 30 s of fake time later.
    /// </summary>
    public async Task StartReloaderAsync()
    {
        _reloader = _services.GetServices<IHostedService>().OfType<TrackingGateReloader>().Single();
        await _reloader.StartAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// Waits (on the wall clock, briefly) until <paramref name="condition"/> holds, e.g. for a reload the fake clock
    /// triggered.
    /// </summary>
    public static async Task WaitUntilAsync(Func<bool> condition, string description)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Timed out waiting until {description}.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(10), TestContext.Current.CancellationToken);
        }
    }

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

    public Task<RoleSetting> SeedRoleSettingAsync(RoleSettingBuilder builder) =>
        InScopeAsync((db, ids) => builder.SeedAsync(db, ids, TestContext.Current.CancellationToken));

    public Task<List<TrackingPeriod>> PeriodsAsync() =>
        InScopeAsync((db, _) => db.TrackingPeriods.AsNoTracking().OrderBy(p => p.Id).ToListAsync(TestContext.Current.CancellationToken));

    public async ValueTask DisposeAsync()
    {
        if (_reloader is not null)
        {
            await _reloader.StopAsync(CancellationToken.None);
        }

        await _services.DisposeAsync();
        Lifetime.Dispose();
    }
}
