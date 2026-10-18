namespace Apollo.Web.Client.Contracts;

/// <summary><see cref="ActivityCountsDto"/> per day (<see cref="ActivityStatsDto.Averages"/>).</summary>
public sealed record ActivityAveragesDto(double Messages, double Characters, double ReactionsGiven, double ReactionsReceived, double VoiceSeconds);
