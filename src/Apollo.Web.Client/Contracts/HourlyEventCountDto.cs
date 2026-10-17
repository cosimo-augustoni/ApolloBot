namespace Apollo.Web.Client.Contracts;

/// <summary>The events recorded in one hour of <see cref="BotStatusDto.EventsPerHour"/>.</summary>
/// <param name="HourStart">The start of the hour (UTC), a full hour in <c>Europe/Zurich</c>.</param>
/// <param name="Count">Rows recorded in the raw event tables in that hour.</param>
public sealed record HourlyEventCountDto(DateTimeOffset HourStart, int Count);
