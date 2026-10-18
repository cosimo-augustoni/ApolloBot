using System.Globalization;
using Apollo.Stats.Raw;

namespace Apollo.Stats.Tests.Raw;

public sealed class StatsRangeTests
{
    [Fact]
    public void ForZurichDays_ShouldRunFromMidnightOfFromToMidnightAfterTo() =>
        StatsRange.ForZurichDays(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 14))
            .ShouldBe(new StatsRange(Utc("2026-01-09T23:00:00Z"), Utc("2026-01-14T23:00:00Z")));

    [Fact]
    public void ForZurichDays_WhenFromIsAfterTo_ShouldThrow() =>
        Should.Throw<ArgumentException>(() => StatsRange.ForZurichDays(new DateOnly(2026, 1, 14), new DateOnly(2026, 1, 10)));

    [Fact]
    public void ForZurichDays_OnTheSpringForwardDay_ShouldLast23Hours()
    {
        var range = StatsRange.ForZurichDays(new DateOnly(2026, 3, 29), new DateOnly(2026, 3, 29));

        (range.Start, range.End - range.Start).ShouldBe((Utc("2026-03-28T23:00:00Z"), TimeSpan.FromHours(23)));
    }

    [Fact]
    public void ForZurichDays_OnTheFallBackDay_ShouldLast25Hours()
    {
        var range = StatsRange.ForZurichDays(new DateOnly(2026, 10, 25), new DateOnly(2026, 10, 25));

        (range.Start, range.End - range.Start).ShouldBe((Utc("2026-10-24T22:00:00Z"), TimeSpan.FromHours(25)));
    }

    [Theory]
    [InlineData("2026-01-10T10:00:00Z", "2026-01-10T11:00:00Z", 60)] // inside
    [InlineData("2026-01-09T22:00:00Z", "2026-01-10T01:00:00Z", 120)] // starts before the range
    [InlineData("2026-01-14T22:00:00Z", "2026-01-15T01:00:00Z", 60)] // ends after the range
    [InlineData("2026-01-09T20:00:00Z", "2026-01-09T23:00:00Z", 0)] // ends where the range starts
    [InlineData("2026-01-15T00:00:00Z", "2026-01-15T01:00:00Z", 0)] // after the range
    public void Clip_ShouldReturnThePartInsideTheRange(string start, string end, int expectedMinutes) =>
        StatsRange.ForZurichDays(new DateOnly(2026, 1, 10), new DateOnly(2026, 1, 14))
            .Clip(Utc(start), Utc(end))
            .ShouldBe(TimeSpan.FromMinutes(expectedMinutes));

    [Fact]
    public void Clip_OnTheSpringForwardDay_ShouldKeepAtMost23Hours()
    {
        // 28 Mar 22:00 CET to 30 Mar 01:00 CEST: only Sunday 29 March, which has 23 hours.
        var range = StatsRange.ForZurichDays(new DateOnly(2026, 3, 29), new DateOnly(2026, 3, 29));

        range.Clip(Utc("2026-03-28T21:00:00Z"), Utc("2026-03-29T23:00:00Z")).ShouldBe(TimeSpan.FromHours(23));
    }

    private static DateTimeOffset Utc(string value) => DateTimeOffset.Parse(value, CultureInfo.InvariantCulture);
}
