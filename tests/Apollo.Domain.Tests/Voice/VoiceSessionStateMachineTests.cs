using Apollo.Domain.Voice;
using Apollo.Testing;

namespace Apollo.Domain.Tests.Voice;

public sealed class VoiceSessionStateMachineTests
{
    private static readonly Guid _alice = Guid.NewGuid();
    private static readonly Guid _bob = Guid.NewGuid();
    private static readonly Guid _lounge = Guid.NewGuid();
    private static readonly Guid _gaming = Guid.NewGuid();

    private static readonly DateTimeOffset _joinedAt = TestTime.Now;
    private static readonly DateTimeOffset _later = TestTime.Now.AddMinutes(5);

    private static readonly TimeSpan _maxGap = TimeSpan.FromMinutes(2);

    private static readonly VoiceFlags _muted = VoiceFlags.None with { SelfMute = true };

    private readonly VoiceSessionStateMachine _machine = new();

    [Fact]
    public void Update_WhenUserJoins_ShouldOpenASessionWithTheirFlags()
    {
        var changes = _machine.Update(_alice, In(_lounge, _muted), _joinedAt);

        changes.ShouldBe([new VoiceSessionOpened(_alice, _joinedAt, _lounge, _muted)]);
    }

    [Fact]
    public void Update_WhenUserLeaves_ShouldCloseTheSessionAsLeft()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);

        var changes = _machine.Update(_alice, Out(), _later);

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Left)]);
    }

    [Fact]
    public void Update_WhenUserMoves_ShouldCloseTheOldSessionAsMovedAndOpenANewOneAtTheSameInstant()
    {
        _machine.Update(_alice, In(_lounge, _muted), _joinedAt);

        var changes = _machine.Update(_alice, In(_gaming, _muted), _later);

        changes.ShouldBe([
            new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Moved),
            new VoiceSessionOpened(_alice, _later, _gaming, _muted),
        ]);
    }

    public static TheoryData<string> FlagChanges => ["mute", "deaf", "stream", "video"];

    [Theory]
    [MemberData(nameof(FlagChanges))]
    public void Update_WhenAFlagChanges_ShouldStartANewSegmentAndKeepTheSession(string flag)
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);
        var flags = flag switch
        {
            "mute" => VoiceFlags.None with { SelfMute = true },
            "deaf" => VoiceFlags.None with { SelfDeaf = true },
            "stream" => VoiceFlags.None with { Streaming = true },
            _ => VoiceFlags.None with { Video = true },
        };

        var changes = _machine.Update(_alice, In(_lounge, flags), _later);

        changes.ShouldBe([new VoiceSegmentChanged(_alice, _later, flags)]);
        _machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public void Update_WhenNothingRelevantChanged_ShouldChangeNothing()
    {
        _machine.Update(_alice, In(_lounge, _muted), _joinedAt);

        _machine.Update(_alice, In(_lounge, _muted), _later).ShouldBeEmpty();
    }

    [Fact]
    public void Update_WhenAUserNotInVoiceLeaves_ShouldChangeNothing()
    {
        _machine.Update(_alice, Out(), _joinedAt).ShouldBeEmpty();
    }

    [Fact]
    public void OpenSessionCount_ShouldCountUsersInVoice()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);
        _machine.Update(_bob, In(_gaming), _joinedAt);
        _machine.Update(_bob, Out(), _later);

        _machine.OpenSessionCount.ShouldBe(1);
        _machine.UsersInVoice.ShouldBe([_alice]);
    }

    [Fact]
    public void Update_WhenASecondConnectionJoins_ShouldKeepTheSessionOfTheFirst()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);

        _machine.Update(_alice, In(_gaming, _muted, connectionId: "b"), _later).ShouldBeEmpty();
        _machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public void Update_WhenOneOfTwoConnectionsInTheSameChannelLeaves_ShouldKeepTheSession()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);
        _machine.Update(_alice, In(_lounge, connectionId: "b"), _joinedAt);

        _machine.Update(_alice, Out(connectionId: "a"), _later).ShouldBeEmpty();
    }

    [Fact]
    public void Update_WhenTheFirstConnectionLeavesAndTheOtherIsInAnotherChannel_ShouldMoveToIt()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);
        _machine.Update(_alice, In(_gaming, _muted, connectionId: "b"), _joinedAt);

        var changes = _machine.Update(_alice, Out(connectionId: "a"), _later);

        changes.ShouldBe([
            new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Moved),
            new VoiceSessionOpened(_alice, _later, _gaming, _muted),
        ]);
    }

    [Fact]
    public void Update_WhenTheLastConnectionLeaves_ShouldCloseTheSessionAsLeft()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);
        _machine.Update(_alice, In(_lounge, connectionId: "b"), _joinedAt);
        _machine.Update(_alice, Out(connectionId: "a"), _later);

        var changes = _machine.Update(_alice, Out(connectionId: "b"), _later);

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Left)]);
    }

    [Fact]
    public void Update_WhenTheSecondConnectionChangesFlags_ShouldChangeNothing()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);
        _machine.Update(_alice, In(_lounge, connectionId: "b"), _joinedAt);

        _machine.Update(_alice, In(_lounge, _muted, connectionId: "b"), _later).ShouldBeEmpty();
    }
    [Fact]
    public void Reconcile_WhenUserIsInVoiceWithoutASession_ShouldOpenOneNow()
    {
        var changes = _machine.Reconcile(_alice, [In(_lounge, _muted)], Outage(lastKnownAt: _joinedAt, now: _later));

        changes.ShouldBe([new VoiceSessionOpened(_alice, _later, _lounge, _muted)]);
    }

    [Fact]
    public void Reconcile_WhenTheMemberIsInTheSameChannelAfterAShortBlindSpot_ShouldContinueTheSession()
    {
        _machine.Update(_alice, In(_lounge, _muted), _joinedAt);

        _machine.Reconcile(_alice, [In(_lounge, _muted)], Outage(lastKnownAt: _later, now: _later + _maxGap)).ShouldBeEmpty();
        _machine.OpenSessionCount.ShouldBe(1);
    }

    [Fact]
    public void Reconcile_WhenTheFlagsChangedDuringAShortBlindSpot_ShouldStartANewSegmentNow()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);
        var now = _later + TimeSpan.FromMinutes(1);

        var changes = _machine.Reconcile(_alice, [In(_lounge, _muted)], Outage(lastKnownAt: _later, now: now));

        changes.ShouldBe([new VoiceSegmentChanged(_alice, now, _muted)]);
    }

    [Fact]
    public void Reconcile_WhenTheBlindSpotIsLongerThanMaxGap_ShouldCloseAtLastKnownAsUnobservedAndOpenANewSessionNow()
    {
        _machine.Update(_alice, In(_lounge, _muted), _joinedAt);
        var now = _later + _maxGap + TimeSpan.FromSeconds(1);

        var changes = _machine.Reconcile(_alice, [In(_lounge, _muted)], Outage(lastKnownAt: _later, now: now));

        changes.ShouldBe([
            new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Unobserved),
            new VoiceSessionOpened(_alice, now, _lounge, _muted),
        ]);
    }

    [Fact]
    public void Reconcile_WhenTheMemberLeft_ShouldCloseAtLastKnownAsUnobserved()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);

        var changes = _machine.Reconcile(_alice, [], Outage(lastKnownAt: _later, now: _later + TimeSpan.FromSeconds(30)));

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Unobserved)]);
        _machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public void Reconcile_WhenTheMemberIsInAnotherChannel_ShouldCloseAtLastKnownAsUnobservedAndOpenANewSessionNow()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);
        var now = _later + TimeSpan.FromSeconds(30);

        var changes = _machine.Reconcile(_alice, [In(_gaming)], Outage(lastKnownAt: _later, now: now));

        changes.ShouldBe([
            new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Unobserved),
            new VoiceSessionOpened(_alice, now, _gaming, VoiceFlags.None),
        ]);
    }

    [Fact]
    public void Reconcile_WhileConnected_WhenTheMemberIsInTheSameChannel_ShouldContinueWhateverTheGap()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);

        _machine.Reconcile(_alice, [In(_lounge)], VoiceBlindSpot.WhileConnected(_joinedAt, _joinedAt.AddHours(5))).ShouldBeEmpty();
    }

    [Fact]
    public void Reconcile_WhenTheBlindSpotIsUnknown_ShouldCloseAtTheGivenTimeAndOpenANewSessionNow()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);

        var changes = _machine.Reconcile(_alice, [In(_lounge)], VoiceBlindSpot.Unknown(closeAt: _later, now: _later));

        changes.ShouldBe([
            new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Unobserved),
            new VoiceSessionOpened(_alice, _later, _lounge, VoiceFlags.None),
        ]);
    }

    [Fact]
    public void Reconcile_WhenRunTwiceOverTheSameLongBlindSpot_ShouldKeepWhatTheFirstRunOpened()
    {
        _machine.Restore(_alice, _lounge, VoiceFlags.None, segmentStartedAt: _joinedAt);
        var blindSpot = Outage(lastKnownAt: _joinedAt, now: _later);
        _machine.Reconcile(_alice, [In(_lounge)], blindSpot);

        _machine.Reconcile(_alice, [In(_lounge)], blindSpot with { }).ShouldBeEmpty();
    }

    [Fact]
    public void Reconcile_WhenRunTwiceWithAnUnknownBlindSpot_ShouldKeepWhatTheFirstRunOpened()
    {
        _machine.Restore(_alice, _lounge, VoiceFlags.None, segmentStartedAt: _joinedAt);
        _machine.Reconcile(_alice, [In(_lounge)], VoiceBlindSpot.Unknown(closeAt: _later, now: _later));

        _machine.Reconcile(_alice, [In(_lounge)], VoiceBlindSpot.Unknown(closeAt: _later, now: _later.AddSeconds(1))).ShouldBeEmpty();
    }

    [Fact]
    public void Reconcile_WhenTheCurrentSegmentStartedAfterLastKnown_ShouldCloseAtTheSegmentStart()
    {
        _machine.Update(_alice, In(_lounge), _later);

        var changes = _machine.Reconcile(_alice, [], VoiceBlindSpot.WhileConnected(lastKnownAt: _joinedAt, now: _later.AddMinutes(1)));

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Unobserved)]);
    }

    [Fact]
    public void Reconcile_ShouldReplaceAllConnectionsOfTheUser()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);
        _machine.Reconcile(_alice, [In(_lounge, connectionId: "b")], Outage(lastKnownAt: _later, now: _later));

        var changes = _machine.Update(_alice, Out(connectionId: "b"), _later);

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Left)]);
    }

    [Fact]
    public void ReconcileUntracked_WhenTheMemberHasASession_ShouldCloseItAtLastKnownAsTrackingStoppedAndForgetThem()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);

        var changes = _machine.ReconcileUntracked(_alice, Outage(lastKnownAt: _later, now: _later.AddMinutes(1)));

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.TrackingStopped)]);
        _machine.OpenSessionCount.ShouldBe(0);
    }

    [Fact]
    public void Restore_ShouldHoldTheTakenOverSessionForTheRule()
    {
        _machine.Restore(_alice, _lounge, _muted, segmentStartedAt: _joinedAt);

        _machine.UsersInVoice.ShouldBe([_alice]);
        _machine.Reconcile(_alice, [In(_lounge, _muted)], Outage(lastKnownAt: _later, now: _later)).ShouldBeEmpty();
        _machine.Update(_alice, Out(), _later).ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Left)]);
    }

    [Fact]
    public void Stop_WhenUserIsInVoice_ShouldCloseTheSessionWithTheReasonAndForgetTheUser()
    {
        _machine.Update(_alice, In(_lounge), _joinedAt);

        var changes = _machine.Stop(_alice, _later, VoiceSessionEndReason.TrackingStopped);

        changes.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.TrackingStopped)]);
        _machine.Update(_alice, Out(), _later).ShouldBeEmpty();
    }

    [Fact]
    public void Stop_WhenUserIsNotInVoice_ShouldChangeNothing()
    {
        _machine.Stop(_alice, _later, VoiceSessionEndReason.TrackingStopped).ShouldBeEmpty();
    }


    [Fact]
    public void Clone_ShouldCopyTheStateWithoutSharingIt()
    {
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _joinedAt);

        var copy = _machine.Clone();
        var copyChanges = copy.Update(_alice, Out(connectionId: "a"), _later);
        copy.Update(_bob, In(_gaming), _later);

        copyChanges.ShouldBe([new VoiceSessionClosed(_alice, _later, VoiceSessionEndReason.Left)]);
        copy.UsersInVoice.ShouldBe([_bob]);
        _machine.UsersInVoice.ShouldBe([_alice]);
        _machine.Update(_alice, In(_lounge, connectionId: "a"), _later).ShouldBeEmpty();
    }

    private static VoiceBlindSpot Outage(DateTimeOffset lastKnownAt, DateTimeOffset now) =>
        VoiceBlindSpot.Outage(lastKnownAt, now, _maxGap);

    private static VoiceConnection In(Guid channelId, VoiceFlags? flags = null, string? connectionId = null) =>
        new(connectionId, channelId, flags ?? VoiceFlags.None);

    private static VoiceConnection Out(string? connectionId = null) => new(connectionId, ChannelId: null, VoiceFlags.None);
}
