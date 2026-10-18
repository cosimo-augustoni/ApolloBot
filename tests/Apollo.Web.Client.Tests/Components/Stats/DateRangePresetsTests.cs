using Apollo.Web.Client.Components.Stats;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Web.Client.Tests.Components.Stats;

/// <summary>The range presets of the stats pages (#37): calendar month and year up to today in <c>Europe/Zurich</c>.</summary>
public sealed class DateRangePresetsTests
{
    [Theory]
    // 10:00 in Zurich (UTC+1).
    [InlineData("2026-01-15T09:00:00Z", "2026-01-01", "2026-01-15")]
    // 00:30 on 1 February in Zurich, still 31 January in UTC.
    [InlineData("2026-01-31T23:30:00Z", "2026-02-01", "2026-02-01")]
    // 23:30 on 31 March in Zurich (UTC+2, summer time since 29 March).
    [InlineData("2026-03-31T21:30:00Z", "2026-03-01", "2026-03-31")]
    // 00:30 on 1 July in Zurich (UTC+2), 30 June in UTC.
    [InlineData("2026-06-30T22:30:00Z", "2026-07-01", "2026-07-01")]
    public void Resolve_WhenMonth_ShouldSelectTheZurichCalendarMonthUpToToday(string now, string from, string to)
    {
        var range = DateRangePresets.Resolve(DateRangePreset.Month, At(now));

        range.ShouldBe(new DateRangeSelection(DateRangePreset.Month, DateOnly.Parse(from), DateOnly.Parse(to)));
    }

    [Theory]
    [InlineData("2026-10-18T12:00:00Z", "2026-01-01", "2026-10-18")]
    // 00:30 on 1 January 2026 in Zurich, still 2025 in UTC.
    [InlineData("2025-12-31T23:30:00Z", "2026-01-01", "2026-01-01")]
    // 23:30 on 31 December 2025 in Zurich.
    [InlineData("2025-12-31T22:30:00Z", "2025-01-01", "2025-12-31")]
    public void Resolve_WhenYear_ShouldSelectTheZurichCalendarYearUpToToday(string now, string from, string to)
    {
        var range = DateRangePresets.Resolve(DateRangePreset.Year, At(now));

        range.ShouldBe(new DateRangeSelection(DateRangePreset.Year, DateOnly.Parse(from), DateOnly.Parse(to)));
    }

    [Fact]
    public void Default_ShouldBeTheMonth()
    {
        var time = At("2026-01-15T09:00:00Z");

        DateRangePresets.Default(time).ShouldBe(DateRangePresets.Resolve(DateRangePreset.Month, time));
    }

    [Fact]
    public void Resolve_WhenCustom_ShouldThrow()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => DateRangePresets.Resolve(DateRangePreset.Custom, At("2026-01-15T09:00:00Z")));
    }

    [Fact]
    public void Custom_ShouldKeepTheGivenDays()
    {
        DateRangeSelection.Custom(new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 9))
            .ShouldBe(new DateRangeSelection(DateRangePreset.Custom, new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 9)));
    }

    private static FakeTimeProvider At(string utc) => new(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));
}
