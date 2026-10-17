namespace Apollo.Bot.Tests.Tracking;

/// <summary>
/// A scoped service a lifecycle handler can dirty, to tell whether two hook calls shared a DI scope.
/// </summary>
internal sealed class ScopeProbe
{
    public bool Dirty { get; set; }
}
