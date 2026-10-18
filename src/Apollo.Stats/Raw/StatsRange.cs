using Apollo.Stats.Activity;

namespace Apollo.Stats.Raw;

/// <summary>
/// The UTC instants of a Zurich date range, <c>[midnight of From, midnight after To)</c>, as #35 reads it (scope §5.3).
/// A DST day lasts 23 or 25 hours.
/// </summary>
public readonly record struct StatsRange(DateTimeOffset Start, DateTimeOffset End)
{
    /// <exception cref="ArgumentException"><paramref name="from"/> is after <paramref name="to"/>.</exception>
    public static StatsRange ForZurichDays(DateOnly from, DateOnly to)
    {
        if (from > to)
        {
            throw new ArgumentException($"From ({from:O}) must not be after To ({to:O}).", nameof(from));
        }

        return new StatsRange(ZurichCalendar.StartOf(from), ZurichCalendar.StartOf(to.AddDays(1)));
    }

    /// <summary>
    /// The part of <c>[start, end)</c> inside the range; zero when they don't overlap.
    /// </summary>
    public TimeSpan Clip(DateTimeOffset start, DateTimeOffset end)
    {
        var from = start > Start ? start : Start;
        var to = end < End ? end : End;
        return to > from ? to - from : TimeSpan.Zero;
    }
}
