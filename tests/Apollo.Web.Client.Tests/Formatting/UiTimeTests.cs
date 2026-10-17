using Apollo.Web.Client.Formatting;

namespace Apollo.Web.Client.Tests.Formatting;

public sealed class UiTimeTests
{
    [Theory]
    [InlineData("2026-01-15T09:00:00Z", "15.01.2026 10:00")]
    [InlineData("2026-10-15T08:30:00Z", "15.10.2026 10:30")]
    [InlineData("2026-12-31T23:30:00Z", "01.01.2027 00:30")]
    public void FormatDateTime_ShouldShowTheZurichTime(string utc, string expected)
    {
        var text = UiTime.FormatDateTime(DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture));

        text.ShouldBe(expected);
    }

    [Theory]
    [InlineData("2026-01-15T09:02:11Z", "10:02:11")]
    [InlineData("2026-07-15T09:02:11Z", "11:02:11")]
    public void FormatTimeWithSeconds_ShouldShowTheZurichClockTime(string utc, string expected) =>
        UiTime.FormatTimeWithSeconds(Parse(utc)).ShouldBe(expected);

    [Theory]
    [InlineData("2026-01-15T09:02:11Z", "2026-01-15T09:30:00Z", "10:02")]
    [InlineData("2026-01-14T22:59:00Z", "2026-01-15T09:30:00Z", "14.01.2026 23:59")]
    [InlineData("2026-01-14T23:00:00Z", "2026-01-15T09:30:00Z", "00:00")]
    public void FormatSince_ShouldShowOnlyTheTimeOnTheSameZurichDay(string utc, string nowUtc, string expected) =>
        UiTime.FormatSince(Parse(utc), Parse(nowUtc)).ShouldBe(expected);

    private static DateTimeOffset Parse(string utc) => DateTimeOffset.Parse(utc, System.Globalization.CultureInfo.InvariantCulture);
}
