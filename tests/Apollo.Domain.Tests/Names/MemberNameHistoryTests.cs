using Apollo.Domain.Names;

namespace Apollo.Domain.Tests.Names;

public sealed class MemberNameHistoryTests
{
    private static readonly DateTimeOffset _trackedSince = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset _now = _trackedSince.AddDays(3);

    private static readonly MemberNames _alice = new(Nickname: null, GlobalName: "Alice", Username: "alice");

    [Fact]
    public void Next_WhenNothingIsStored_ShouldBeABaselineWithTheObservedNames()
    {
        var next = MemberNameHistory.Next(_alice, _now, _trackedSince, latest: null);

        next.ShouldBe(new MemberNameEntry(MemberNameChangeKind.Baseline, _alice, _now));
    }

    [Fact]
    public void Next_WhenTheLatestRowIsFromAnEarlierTrackingPeriod_ShouldBeABaselineEvenWithTheSameNames()
    {
        var next = MemberNameHistory.Next(_alice, _now, _trackedSince, new RecordedMemberNames(_alice, _trackedSince.AddDays(-5)));

        next.ShouldBe(new MemberNameEntry(MemberNameChangeKind.Baseline, _alice, _now));
    }

    [Fact]
    public void Next_WhenObservedBeforeTheTrackingStarted_ShouldStampTheBaselineAtTheStart()
    {
        // An update received just before the gate tracked the member, handled once it did.
        var next = MemberNameHistory.Next(_alice, _trackedSince.AddSeconds(-2), _trackedSince, latest: null);

        next.ShouldBe(new MemberNameEntry(MemberNameChangeKind.Baseline, _alice, _trackedSince));
    }

    [Fact]
    public void Next_WhenTheNamesMatchTheLatestRow_ShouldBeNothing()
    {
        var next = MemberNameHistory.Next(_alice, _now, _trackedSince, new RecordedMemberNames(_alice, _trackedSince));

        next.ShouldBeNull();
    }

    [Theory]
    [InlineData("Ali", "Alice", "alice")]
    [InlineData(null, "Alicia", "alice")]
    [InlineData(null, null, "alice")]
    [InlineData(null, "Alice", "alice_2")]
    public void Next_WhenOneNameDiffersFromTheLatestRow_ShouldBeAChangeWithTheObservedNames(string? nickname, string? globalName, string username)
    {
        var observed = new MemberNames(nickname, globalName, username);

        var next = MemberNameHistory.Next(observed, _now, _trackedSince, new RecordedMemberNames(_alice, _trackedSince));

        next.ShouldBe(new MemberNameEntry(MemberNameChangeKind.Changed, observed, _now));
    }

    [Fact]
    public void Next_WhenTheNicknameIsRemoved_ShouldBeAChangeWithoutNickname()
    {
        var withNickname = _alice with { Nickname = "Ali" };

        var next = MemberNameHistory.Next(_alice, _now, _trackedSince, new RecordedMemberNames(withNickname, _trackedSince));

        next.ShouldBe(new MemberNameEntry(MemberNameChangeKind.Changed, _alice, _now));
    }

    [Fact]
    public void Next_WhenObservedBeforeTheLatestRow_ShouldBeNothing()
    {
        // The update waited while a sync stored newer names: they already include it, so it must not undo them.
        var latest = new RecordedMemberNames(_alice with { Nickname = "Al" }, _now);

        var next = MemberNameHistory.Next(_alice with { Nickname = "Ali" }, _now.AddSeconds(-1), _trackedSince, latest);

        next.ShouldBeNull();
    }

    [Fact]
    public void Next_WhenObservedAtTheSameInstantAsTheLatestRow_ShouldStillCompare()
    {
        var latest = new RecordedMemberNames(_alice, _now);

        var next = MemberNameHistory.Next(_alice with { Nickname = "Ali" }, _now, _trackedSince, latest);

        next.ShouldBe(new MemberNameEntry(MemberNameChangeKind.Changed, _alice with { Nickname = "Ali" }, _now));
    }
}
