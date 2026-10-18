namespace Apollo.Web.Client.Contracts;

/// <summary>The rows of one raw event table in one hour of <see cref="BotStatusDto.EventsPerHour"/>.</summary>
/// <param name="Type">The table.</param>
/// <param name="Count">Its rows in that hour.</param>
public sealed record EventTypeCountDto(DataEventType Type, int Count);
