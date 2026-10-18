using Apollo.Web.Client.Formatting;

namespace Apollo.Web.Client.Tests.Formatting;

public sealed class UiDurationTests
{
    [Theory]
    [InlineData(0, "0 min")]
    [InlineData(59, "0 min")]
    [InlineData(35 * 60, "35 min")]
    [InlineData(3600, "1 h 0 min")]
    [InlineData((3 * 3600) + (25 * 60) + 59, "3 h 25 min")]
    [InlineData((1234L * 3600) + (5 * 60), "1'234 h 5 min")]
    public void HoursAndMinutes_ShouldShowWholeHoursAndMinutes(long seconds, string expected) =>
        UiDuration.HoursAndMinutes(seconds).ShouldBe(expected);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(5400, 1.5)]
    [InlineData(3599, 1.0)]
    [InlineData(100, 0.0)]
    public void Hours_ShouldRoundToOneDecimal(long seconds, double expected) =>
        UiDuration.Hours(seconds).ShouldBe(expected);
}
