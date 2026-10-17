namespace Apollo.Web.Client.Contracts;

/// <summary>The rows of one raw event table in the range of <see cref="EventCountsDto"/>.</summary>
/// <param name="Type">The table.</param>
/// <param name="Total">All rows in the range.</param>
/// <param name="Days">Every day of the range, oldest first, days without rows as 0.</param>
public sealed record EventTableCountDto(DataEventType Type, int Total, IReadOnlyList<DailyEventCountDto> Days);
