using Apollo.Bot.GuildState;

namespace Apollo.Bot.Tests.GuildState;

/// <summary>
/// Every guild state notification a test host published, in order.
/// </summary>
internal sealed class NotificationLog
{
    private readonly Lock _lock = new();
    private readonly List<GuildStateNotification> _notifications = [];

    public IReadOnlyList<GuildStateNotification> All
    {
        get
        {
            lock (_lock)
            {
                return [.. _notifications];
            }
        }
    }

    public IReadOnlyList<Guid> MemberChanges => [.. All.OfType<MemberAccessStateChanged>().Select(n => n.UserId)];

    public void Add(GuildStateNotification notification)
    {
        lock (_lock)
        {
            _notifications.Add(notification);
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _notifications.Clear();
        }
    }
}
