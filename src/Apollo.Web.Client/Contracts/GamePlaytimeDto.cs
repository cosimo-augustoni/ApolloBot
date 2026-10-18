namespace Apollo.Web.Client.Contracts;

/// <summary>The time spent playing <paramref name="Name"/> inside the range.</summary>
public sealed record GamePlaytimeDto(string Name, long Seconds);
