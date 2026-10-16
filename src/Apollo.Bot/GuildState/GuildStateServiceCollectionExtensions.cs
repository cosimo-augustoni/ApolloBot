using Apollo.Platform.Abstractions.Dispatch;
using Apollo.Platform.Abstractions.Events;

namespace Apollo.Bot.GuildState;

public static class GuildStateServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="GuildStateSync"/> for the guild snapshot and the member, role and channel events (#13).
    /// Needs an <see cref="Apollo.Platform.Abstractions.IChatPlatform"/>, the <c>ApolloDbContext</c> and
    /// <c>IPlatformIdResolver</c>.
    /// </summary>
    public static IServiceCollection AddGuildStateSync(this IServiceCollection services)
    {
        services.AddPlatformEventDispatcher();
        services.AddSingleton<GuildStateSyncLock>();
        services.AddScoped<GuildStateNotificationPublisher>();
        services.AddScoped<GuildStateSync>();

        AddSyncHandler<PlatformGuildAvailable>(services);
        AddSyncHandler<PlatformMemberJoined>(services);
        AddSyncHandler<PlatformMemberUpdated>(services);
        AddSyncHandler<PlatformMemberLeft>(services);
        AddSyncHandler<PlatformRoleCreated>(services);
        AddSyncHandler<PlatformRoleUpdated>(services);
        AddSyncHandler<PlatformRoleDeleted>(services);
        AddSyncHandler<PlatformChannelCreated>(services);
        AddSyncHandler<PlatformChannelUpdated>(services);
        AddSyncHandler<PlatformChannelDeleted>(services);

        return services;
    }

    /// <summary>
    /// Registers a scoped handler for <typeparamref name="TNotification"/>. Handlers run in registration order.
    /// </summary>
    public static IServiceCollection AddGuildStateNotificationHandler<TNotification, THandler>(this IServiceCollection services)
        where TNotification : GuildStateNotification
        where THandler : class, IGuildStateNotificationHandler<TNotification>
    {
        services.AddScoped<IGuildStateNotificationHandler<TNotification>, THandler>();
        return services;
    }

    // One GuildStateSync per event scope, whichever event it handles.
    private static void AddSyncHandler<TEvent>(IServiceCollection services)
        where TEvent : PlatformEvent =>
        services.AddScoped<IPlatformEventHandler<TEvent>>(serviceProvider => (IPlatformEventHandler<TEvent>)(object)serviceProvider.GetRequiredService<GuildStateSync>());
}
