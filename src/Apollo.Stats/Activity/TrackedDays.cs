namespace Apollo.Stats.Activity;

/// <summary>
/// The days a user was tracked (scope §5.1.1), so averages leave untracked time out.
/// </summary>
public static class TrackedDays
{
    /// <summary>
    /// The Zurich days from <paramref name="from"/> to <paramref name="to"/> (both inclusive) that overlap at least one
    /// of <paramref name="periods"/>; an open period runs until <paramref name="now"/>.
    /// </summary>
    public static int Count(IEnumerable<TrackingSpan> periods, DateOnly from, DateOnly to, DateTimeOffset now)
    {
        var spans = periods.ToList();
        var count = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            var dayStart = ZurichCalendar.StartOf(day);
            var dayEnd = ZurichCalendar.StartOf(day.AddDays(1));
            if (spans.Any(p => p.From < dayEnd && (p.To ?? now) > dayStart))
            {
                count++;
            }
        }

        return count;
    }
}
