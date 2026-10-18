namespace Apollo.Web.Client.Contracts;

/// <summary>One Zurich day of <see cref="ActivityStatsDto.Daily"/>.</summary>
public sealed record DailyActivityDto(DateOnly Date, ActivityCountsDto Counts);
