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
}
