using Apollo.Stats.Sanity;
using Apollo.Testing;
using Microsoft.Extensions.Time.Testing;

namespace Apollo.Stats.Tests.Sanity;

public sealed class DataSanityQueryTests
{
    [Fact]
    public void DefaultRange_ShouldBeTheLast30ZurichDaysUpToToday()
    {
        var range = DataSanityQuery.DefaultRange(TestTime.CreateProvider());

        range.ShouldBe((new DateOnly(2025, 12, 17), new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public void DefaultRange_WhenItIsAlreadyTomorrowInZurich_ShouldEndOnTheZurichDay()
    {
        // 22:30 UTC on 31 March is 00:30 on 1 April in Zurich (summer time).
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 3, 31, 22, 30, 0, TimeSpan.Zero));

        var range = DataSanityQuery.DefaultRange(time);

        range.ShouldBe((new DateOnly(2026, 3, 3), new DateOnly(2026, 4, 1)));
    }
}
