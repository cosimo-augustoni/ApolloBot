namespace Apollo.Web.Client.Contracts;

/// <summary>The rows of one raw event table on one day.</summary>
/// <param name="Day">The <c>Europe/Zurich</c> calendar day (sessions count on the day they started).</param>
/// <param name="Count">The rows that day.</param>
public sealed record DailyEventCountDto(DateOnly Day, int Count);
