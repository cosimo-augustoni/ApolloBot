using Apollo.Web.Client.Formatting;
using Apollo.Web.Client.Resources;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Apollo.Web.Client.Tests.Formatting;

public sealed class LocalizerExtensionsTests : ApolloTestContext
{
    private static readonly DateTimeOffset _now = new(2026, 1, 15, 9, 30, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(0, "vor 0 s")]
    [InlineData(12, "vor 12 s")]
    [InlineData(59, "vor 59 s")]
    [InlineData(60, "vor 1 min")]
    [InlineData(28 * 60 + 59, "vor 28 min")]
    [InlineData(59 * 60 + 59, "vor 59 min")]
    [InlineData(3600, "vor 1 h")]
    [InlineData(23 * 3600 + 3599, "vor 23 h")]
    [InlineData(24 * 3600, "vor 1 Tag")]
    [InlineData(3 * 24 * 3600, "vor 3 Tagen")]
    [InlineData(-5, "vor 0 s")]
    public void Ago_ShouldShowTheLargestWholeUnit(int secondsAgo, string expected)
    {
        var localizer = Services.GetRequiredService<IStringLocalizer<Strings>>();

        localizer.Ago(_now.AddSeconds(-secondsAgo), _now).ShouldBe(expected);
    }
}
