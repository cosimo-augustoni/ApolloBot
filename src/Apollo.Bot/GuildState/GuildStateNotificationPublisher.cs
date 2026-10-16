namespace Apollo.Bot.GuildState;

/// <summary>
/// Hands notifications to every registered <see cref="IGuildStateNotificationHandler{TNotification}"/>, in
/// registration order. A failing handler fails the publish; the platform event dispatcher logs it.
/// </summary>
internal sealed class GuildStateNotificationPublisher(IServiceProvider services)
{
    public async Task PublishAsync<TNotification>(TNotification notification, CancellationToken cancellationToken)
        where TNotification : GuildStateNotification
    {
        foreach (var handler in services.GetServices<IGuildStateNotificationHandler<TNotification>>())
        {
            await handler.HandleAsync(notification, cancellationToken);
        }
    }
}
