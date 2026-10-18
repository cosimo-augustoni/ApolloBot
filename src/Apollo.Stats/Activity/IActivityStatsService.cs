namespace Apollo.Stats.Activity;

/// <summary>
/// The date-range stats layer (scope §5.3, layer 2): reads the daily activity aggregates (#34) for any Zurich date
/// range. The admin dashboard, the member pages and the recaps all read it.
/// </summary>
public interface IActivityStatsService
{
    /// <exception cref="ArgumentException"><see cref="ActivityStatsQuery.From"/> is after <see cref="ActivityStatsQuery.To"/>.</exception>
    Task<ActivityStatsDto> GetAsync(ActivityStatsQuery query, CancellationToken cancellationToken);
}
