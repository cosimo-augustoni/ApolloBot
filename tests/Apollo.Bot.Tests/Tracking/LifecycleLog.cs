using Apollo.Bot.Tracking;

namespace Apollo.Bot.Tests.Tracking;

/// <summary>
/// Every lifecycle hook call a test host made, in order.
/// </summary>
internal sealed class LifecycleLog
{
    private readonly Lock _lock = new();
    private readonly List<(string Hook, TrackedUser User)> _calls = [];

    public IReadOnlyList<(string Hook, TrackedUser User)> All
    {
        get
        {
            lock (_lock)
            {
                return [.. _calls];
            }
        }
    }

    public IReadOnlyList<Guid> Started => [.. All.Where(c => c.Hook == "started").Select(c => c.User.UserId)];

    public IReadOnlyList<Guid> Stopped => [.. All.Where(c => c.Hook == "stopped").Select(c => c.User.UserId)];

    public void Add(string hook, TrackedUser user)
    {
        lock (_lock)
        {
            _calls.Add((hook, user));
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _calls.Clear();
        }
    }
}
