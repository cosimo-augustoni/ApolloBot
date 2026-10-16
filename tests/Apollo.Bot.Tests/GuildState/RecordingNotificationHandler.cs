using Apollo.Bot.GuildState;

namespace Apollo.Bot.Tests.GuildState;

internal sealed class RecordingNotificationHandler(NotificationLog log)
    : IGuildStateNotificationHandler<MemberAccessStateChanged>, IGuildStateNotificationHandler<GuildStateSynchronized>
{
    public Task HandleAsync(MemberAccessStateChanged notification, CancellationToken cancellationToken)
    {
        log.Add(notification);
        return Task.CompletedTask;
    }

    public Task HandleAsync(GuildStateSynchronized notification, CancellationToken cancellationToken)
    {
        log.Add(notification);
        return Task.CompletedTask;
    }
}
