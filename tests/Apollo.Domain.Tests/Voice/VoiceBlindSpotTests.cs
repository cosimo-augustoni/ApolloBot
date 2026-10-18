using Apollo.Domain.Voice;
using Apollo.Testing;

namespace Apollo.Domain.Tests.Voice;

public sealed class VoiceBlindSpotTests
{
    private static readonly TimeSpan _maxGap = TimeSpan.FromMinutes(2);

    [Fact]
    public void Outage_WhenTheBlindSpotIsExactlyMaxGap_ShouldLetSessionsContinue()
    {
        VoiceBlindSpot.Outage(TestTime.Now, TestTime.Now + _maxGap, _maxGap).Continues(TestTime.Now.AddHours(-1)).ShouldBeTrue();
    }

    [Fact]
    public void Outage_WhenTheBlindSpotIsLongerThanMaxGap_ShouldNotLetSessionsContinue()
    {
        VoiceBlindSpot.Outage(TestTime.Now, TestTime.Now + _maxGap + TimeSpan.FromTicks(1), _maxGap).Continues(TestTime.Now.AddHours(-1)).ShouldBeFalse();
    }

    [Fact]
    public void WhileConnected_ShouldLetSessionsContinueWhateverTheGap()
    {
        VoiceBlindSpot.WhileConnected(TestTime.Now, TestTime.Now.AddDays(1)).Continues(TestTime.Now.AddHours(-1)).ShouldBeTrue();
    }

    [Fact]
    public void Unknown_WhenTheSessionWasLastSeenBeforeTheCloseTime_ShouldNotLetItContinue()
    {
        VoiceBlindSpot.Unknown(TestTime.Now, TestTime.Now).Continues(TestTime.Now.AddHours(-1)).ShouldBeFalse();
    }

    [Fact]
    public void Unknown_WhenTheSessionWasSeenSinceTheCloseTime_ShouldLetItContinue()
    {
        VoiceBlindSpot.Unknown(TestTime.Now, TestTime.Now.AddSeconds(5)).Continues(TestTime.Now).ShouldBeTrue();
    }

    [Fact]
    public void Outage_WhenTheSessionWasSeenAfterLastKnown_ShouldMeasureTheGapFromThen()
    {
        var lastKnownAt = TestTime.Now.AddHours(-1);

        VoiceBlindSpot.Outage(lastKnownAt, TestTime.Now, _maxGap).Continues(TestTime.Now - _maxGap).ShouldBeTrue();
    }

    [Fact]
    public void LastKnownAt_WhenLaterThanNow_ShouldBeNow()
    {
        VoiceBlindSpot.WhileConnected(TestTime.Now.AddMinutes(1), TestTime.Now).LastKnownAt.ShouldBe(TestTime.Now);
    }
}
