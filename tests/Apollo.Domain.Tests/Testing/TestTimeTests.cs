using Apollo.Testing;

namespace Apollo.Domain.Tests.Testing;

public sealed class TestTimeTests
{
    [Fact]
    public void CreateProvider_ShouldStartAtFixedUtcInstant()
    {
        var time = TestTime.CreateProvider();

        time.GetUtcNow().ShouldBe(TestTime.Now);
        TestTime.Now.Offset.ShouldBe(TimeSpan.Zero);
    }

    [Fact]
    public void CreateProvider_ShouldUseZurichWinterTime()
    {
        var time = TestTime.CreateProvider();

        time.LocalTimeZone.ShouldBe(TestTime.Zurich);
        time.GetLocalNow().Offset.ShouldBe(TimeSpan.FromHours(1));
    }
}
