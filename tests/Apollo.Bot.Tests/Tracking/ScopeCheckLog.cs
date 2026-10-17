namespace Apollo.Bot.Tests.Tracking;

/// <summary>
/// Every call of <see cref="ScopeCheckingLifecycleHandler"/>, with whether it found its scope already dirty.
/// </summary>
internal sealed class ScopeCheckLog
{
    private readonly Lock _lock = new();
    private readonly List<(Guid UserId, bool SawDirtyScope)> _calls = [];

    public IReadOnlyList<(Guid UserId, bool SawDirtyScope)> Calls
    {
        get
        {
            lock (_lock)
            {
                return [.. _calls];
            }
        }
    }

    /// <summary>
    /// Records a call; <see langword="true"/> for the first one.
    /// </summary>
    public bool Record(Guid userId, bool sawDirtyScope)
    {
        lock (_lock)
        {
            _calls.Add((userId, sawDirtyScope));
            return _calls.Count == 1;
        }
    }
}
