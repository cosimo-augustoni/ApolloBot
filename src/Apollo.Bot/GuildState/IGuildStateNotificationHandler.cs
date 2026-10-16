namespace Apollo.Bot.GuildState;

/// <summary>
/// Consumes one kind of <see cref="GuildStateNotification"/>. Register with
/// <see cref="GuildStateServiceCollectionExtensions.AddGuildStateNotificationHandler{TNotification, THandler}"/>;
/// handlers are scoped and run in the scope of the platform event that caused the change.
/// </summary>
public interface IGuildStateNotificationHandler<in TNotification>
    where TNotification : GuildStateNotification
{
    Task HandleAsync(TNotification notification, CancellationToken cancellationToken);
}
