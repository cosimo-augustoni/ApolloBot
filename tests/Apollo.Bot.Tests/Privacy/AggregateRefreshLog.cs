namespace Apollo.Bot.Tests.Privacy;

/// <summary>
/// The calls a <see cref="RecordingAggregateRefresher"/> got, and the failure it throws when <see cref="Failure"/> is set.
/// </summary>
internal sealed class AggregateRefreshLog
{
    public List<Call> Calls { get; } = [];

    public Exception? Failure { get; init; }

    /// <summary>
    /// One refresh over <c>From</c> to <c>To</c>; <c>InTransaction</c> says whether the deletion's context had a
    /// transaction open at the call.
    /// </summary>
    public sealed record Call(DateTimeOffset From, DateTimeOffset To, bool InTransaction);
}
