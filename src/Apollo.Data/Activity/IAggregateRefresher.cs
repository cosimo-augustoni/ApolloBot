namespace Apollo.Data.Activity;

/// <summary>
/// Re-materializes every continuous aggregate over a time range now, instead of waiting for the refresh policies, which
/// only cover the last <see cref="ActivityAggregates.RefreshStartOffset"/> (#39).
/// </summary>
public interface IAggregateRefresher
{
    Task RefreshAsync(DateTimeOffset from, DateTimeOffset to, CancellationToken cancellationToken);
}
