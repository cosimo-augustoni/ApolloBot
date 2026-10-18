namespace Apollo.Web.Client.Contracts;

/// <summary>The events recorded in one hour of <see cref="BotStatusDto.EventsPerHour"/>.</summary>
/// <param name="HourStart">The start of the hour (UTC), a full hour in <c>Europe/Zurich</c>.</param>
/// <param name="Count">Rows recorded in the raw event tables in that hour, the sum of <paramref name="Types"/>.</param>
/// <param name="Types">The rows per table, every counted table present (0 when empty), in <see cref="DataEventType"/> order.</param>
public sealed record HourlyEventCountDto(DateTimeOffset HourStart, int Count, IReadOnlyList<EventTypeCountDto> Types)
{
    /// <summary>The rows of <paramref name="type"/> in that hour.</summary>
    public int CountOf(DataEventType type) => Types.FirstOrDefault(t => t.Type == type)?.Count ?? 0;
}
