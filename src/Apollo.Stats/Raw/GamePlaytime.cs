namespace Apollo.Stats.Raw;

/// <summary>
/// The time spent playing <see cref="Name"/> inside the range.
/// </summary>
public sealed record GamePlaytime(string Name, long Seconds);
