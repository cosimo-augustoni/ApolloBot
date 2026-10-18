using System.Globalization;
using Apollo.Stats.Activity;

namespace Apollo.Stats.Tests.Activity;

public sealed class TrackedDaysTests
{
    private static readonly DateTimeOffset _now = Utc("2026-01-15T09:00:00Z"); // 15 Jan 10:00 CET

    [Fact]
    public void Count_WithoutPeriods_ShouldBeZero() =>
        TrackedDays.Count([], new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), _now).ShouldBe(0);

    [Fact]
    public void Count_ShouldCountEveryZurichDayAPeriodOverlaps()
    {
        // 3 Jan 23:30 CET to 5 Jan 00:00 CET: 3 and 4 January.
        TrackingSpan[] periods = [Period("2026-01-03T22:30:00Z", "2026-01-04T23:00:00Z")];

        TrackedDays.Count(periods, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), _now).ShouldBe(2);
    }

    [Fact]
    public void Count_ShouldCountADayWithSeveralPeriodsOnce()
    {
        TrackingSpan[] periods =
        [
            Period("2026-01-03T08:00:00Z", "2026-01-03T09:00:00Z"),
            Period("2026-01-03T10:00:00Z", "2026-01-04T10:00:00Z"),
        ];

        TrackedDays.Count(periods, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), _now).ShouldBe(2);
    }

    [Fact]
    public void Count_ShouldCountOnlyDaysInTheRange()
    {
        TrackingSpan[] periods = [Period("2025-12-01T00:00:00Z", "2026-02-01T00:00:00Z")];

        TrackedDays.Count(periods, new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 12), _now).ShouldBe(3);
    }

    [Fact]
    public void Count_WithAnOpenPeriod_ShouldCountUntilNow()
    {
        TrackingSpan[] periods = [Period("2026-01-09T23:00:00Z", null)]; // from 10 Jan 00:00 CET

        TrackedDays.Count(periods, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), _now).ShouldBe(6);
    }

    [Theory]
    [InlineData("2026-03-28T23:00:00Z", "2026-03-29T22:00:00Z", 1)] // exactly the 23 h spring-forward day
    [InlineData("2026-10-24T22:00:00Z", "2026-10-25T23:00:00Z", 1)] // exactly the 25 h fall-back day
    [InlineData("2026-10-25T22:30:00Z", "2026-10-25T23:30:00Z", 2)] // 25 Oct 23:30 CET to 26 Oct 00:30 CET
    public void Count_OnADstSwitchDay_ShouldUseTheZurichDayBoundaries(string fromUtc, string toUtc, int expected)
    {
        TrackingSpan[] periods = [Period(fromUtc, toUtc)];

        TrackedDays.Count(periods, new DateOnly(2026, 3, 1), new DateOnly(2026, 10, 31), Utc("2027-01-01T00:00:00Z")).ShouldBe(expected);
    }

    private static TrackingSpan Period(string fromUtc, string? toUtc) => new(Utc(fromUtc), toUtc is null ? null : Utc(toUtc));

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
