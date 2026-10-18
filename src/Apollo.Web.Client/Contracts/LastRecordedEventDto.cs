namespace Apollo.Web.Client.Contracts;

/// <summary>The newest row of the raw event tables <see cref="BotStatusDto.EventsPerHour"/> counts (sessions by their start).</summary>
/// <param name="Type">The table of the row.</param>
/// <param name="OccurredAt">When it happened (UTC).</param>
/// <param name="ChannelName">The synced name of its channel; <see langword="null"/> for a table without a channel (membership, presence).</param>
public sealed record LastRecordedEventDto(DataEventType Type, DateTimeOffset OccurredAt, string? ChannelName);
