namespace Apollo.Stats.Tracking;

/// <summary>
/// Whether and since when a member is tracked (scope §5.1.1), from their <c>tracking_periods</c>, as Zurich days.
/// </summary>
/// <param name="TrackedSince">The day their open period started; <see langword="null"/> when they are not tracked now.</param>
/// <param name="TrackedUntil">The day their latest closed period ended; <see langword="null"/> when no period ever closed.</param>
public sealed record TrackingStatus(DateOnly? TrackedSince, DateOnly? TrackedUntil)
{
    /// <summary>Never tracked: no period at all.</summary>
    public static TrackingStatus Never { get; } = new(null, null);

    /// <summary>Whether they have an open tracking period.</summary>
    public bool IsTracked => TrackedSince is not null;

    /// <summary>Whether they had a tracking period at any time, open or closed.</summary>
    public bool WasEverTracked => TrackedSince is not null || TrackedUntil is not null;
}
