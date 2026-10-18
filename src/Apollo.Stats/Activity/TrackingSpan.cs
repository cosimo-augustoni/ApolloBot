namespace Apollo.Stats.Activity;

/// <summary>
/// A span in which a user was tracked (a <c>tracking_periods</c> row, scope §5.1.1); <see cref="To"/> is
/// <see langword="null"/> while it is open.
/// </summary>
public readonly record struct TrackingSpan(DateTimeOffset From, DateTimeOffset? To);
