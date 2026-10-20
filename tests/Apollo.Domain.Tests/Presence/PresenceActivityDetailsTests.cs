using Apollo.Domain.Presence;

namespace Apollo.Domain.Tests.Presence;

public sealed class PresenceActivityDetailsTests
{
    [Fact]
    public void Cut_WhenLongerThanTheLimit_ShouldKeepTheFirst128Characters()
    {
        PresenceActivityDetails.Cut(new string('a', 200)).ShouldBe(new string('a', 128));
    }

    [Fact]
    public void Cut_WhenWithinTheLimit_ShouldKeepTheText()
    {
        PresenceActivityDetails.Cut("In Queue").ShouldBe("In Queue");
        PresenceActivityDetails.Cut(null).ShouldBeNull();
    }

    [Fact]
    public void Cut_WhenTheLimitFallsInsideASurrogatePair_ShouldDropTheWholePair()
    {
        var text = new string('a', 127) + "\U0001F600" + "b";

        PresenceActivityDetails.Cut(text).ShouldBe(new string('a', 127));
    }

    [Fact]
    public void IsSameSegmentAs_WhenOnlyTheApplicationOrTheTimestampsDiffer_ShouldBeTrue()
    {
        var details = new PresenceActivityDetails(Details: "Song", ApplicationId: "1", ReportedStart: DateTimeOffset.UnixEpoch);

        details.IsSameSegmentAs(details with { ApplicationId = "2", ReportedStart = null, ReportedEnd = DateTimeOffset.UnixEpoch }).ShouldBeTrue();
    }

    [Fact]
    public void IsSameSegmentAs_WhenTheTextDiffers_ShouldBeFalse()
    {
        new PresenceActivityDetails(State: "In Queue").IsSameSegmentAs(new PresenceActivityDetails(State: "In Match")).ShouldBeFalse();
    }
}
