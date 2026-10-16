using Apollo.Bot.GuildState;
using Apollo.Data;
using Apollo.Data.Identity;
using Apollo.Platform.Abstractions;
using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;
using Apollo.Testing;
using Apollo.Testing.Data;
using Apollo.Testing.Platform;
using CmdScale.EntityFrameworkCore.TimescaleDB;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Apollo.Bot.Tests.GuildState;

/// <summary>
/// The guild state sync as the bot wires it (<see cref="GuildStateServiceCollectionExtensions.AddGuildStateSync"/>),
/// on the shared Timescale database and behind a <see cref="FakeChatPlatform"/>. Each host has its own
/// <see cref="PlatformIdCache"/>, because Respawn empties the tables between tests.
/// </summary>
internal sealed class SyncHost : IAsyncDisposable
{
    private readonly ServiceProvider _services;

    public SyncHost(TimescaleFixture fixture)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<TimeProvider>(TestTime.CreateProvider());
        services.AddDbContext<ApolloDbContext>(options => options.UseNpgsql(fixture.ConnectionString).UseTimescaleDb());
        services.AddSingleton<PlatformIdCache>();
        services.AddScoped<IPlatformIdResolver, PlatformIdResolver>();
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

    public NotificationLog Notifications { get; } = new();

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

    public ValueTask DisposeAsync() => _services.DisposeAsync();
}
