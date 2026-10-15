using Microsoft.Extensions.Time.Testing;

namespace Apollo.Testing;

/// <summary>
/// Fixed time for tests, so no test depends on the wall clock.
/// </summary>
public static class TestTime
{
    /// <summary>
    /// The time zone stats bucket by (scope §5.3).
    /// </summary>
    public static readonly TimeZoneInfo Zurich = TimeZoneInfo.FindSystemTimeZoneById("Europe/Zurich");

    /// <summary>
    /// The default "now": Thursday 15 January 2026, 09:00 UTC, which is 10:00 in Zurich winter time (UTC+1).
    /// </summary>
    public static readonly DateTimeOffset Now = new(2026, 1, 15, 9, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// A <see cref="FakeTimeProvider"/> at <see cref="Now"/> with <see cref="Zurich"/> as its local time zone.
    /// </summary>
    public static FakeTimeProvider CreateProvider()
    {
        var time = new FakeTimeProvider(Now);
        time.SetLocalTimeZone(Zurich);
        return time;
    }
}
